using System;
using System.Collections.Generic;
using System.Linq;
using DreamGate.Battlegrounds.Combat;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Players;
using DreamGate.Battlegrounds.Services;

namespace DreamGate.Battlegrounds.Networking
{
    public sealed class RatedMatchSession : IDisposable
    {
        private readonly MatchManager manager;
        private readonly Dictionary<string, int> playerIdToSlot = new();
        private readonly Queue<int> pendingHumanCombats = new();
        private int stateVersion;
        private int? activeCombatPlayerId;

        public string LobbyId { get; }
        public int MatchSeed { get; }
        public MatchSlot[] Slots { get; }

        public RatedMatchSession(string lobbyId, int matchSeed, MatchSlot[] slots)
        {
            LobbyId = lobbyId;
            MatchSeed = matchSeed;
            Slots = slots ?? Array.Empty<MatchSlot>();

            GameSfxPlayer.SuppressFeedback = true;
            GameMusicPlayer.SuppressFeedback = true;

            manager = new MatchManager();
            manager.AuthoritativeSimulation = true;
            manager.CombatPlaybackReady += OnCombatPlaybackReady;
            manager.MatchEnded += OnMatchEnded;
            manager.RecruitPhaseEnded += OnRecruitEnded;
            manager.Initialize(MatchMode.Rated, 0, matchSeed, Slots);

            for (var i = 0; i < Slots.Length; i++)
            {
                var slot = Slots[i];
                if (slot != null && !slot.isBot && !string.IsNullOrEmpty(slot.playerId))
                {
                    playerIdToSlot[slot.playerId] = i;
                }
            }
        }

        public void Tick(float deltaTime)
        {
            var before = (int)Math.Ceiling(Math.Max(0d, manager.RecruitTimeRemaining));
            manager.TickRecruitTimer(deltaTime);
            var after = (int)Math.Ceiling(Math.Max(0d, manager.RecruitTimeRemaining));
            if (before != after)
            {
                BumpVersion();
            }
        }

        public MatchSnapshot GetSnapshot(string externalPlayerId)
        {
            var slot = ResolveSlot(externalPlayerId);
            var snapshot = MatchSnapshotBuilder.Build(manager, stateVersion, slot);
            if (manager.Phase == MatchPhase.GameOver)
            {
                var viewer = manager.GetPlayer(slot);
                snapshot.matchEnd = new MatchEndSnapshot
                {
                    playerWon = viewer != null && viewer.placement == 1,
                    placement = viewer?.placement > 0 ? viewer.placement : 8,
                    turnsPlayed = manager.Turn,
                    finalHeroHealth = viewer?.heroHealth ?? 0,
                    damageDealt = viewer?.damageDealt ?? 0,
                    damageTaken = viewer?.damageTaken ?? 0,
                    heroName = viewer?.heroName
                };
            }

            return snapshot;
        }

        public bool TryApplyAction(string externalPlayerId, string action, Dictionary<string, string> payload, out string message)
        {
            message = string.Empty;
            var slot = ResolveSlot(externalPlayerId);
            if (slot < 0)
            {
                message = "Player not in match.";
                return false;
            }

            if (manager.Phase != MatchPhase.Recruit)
            {
                message = "Not in recruit phase.";
                return false;
            }

            var success = action switch
            {
                "buy_shop" => manager.TryBuyFromShop(slot, ReadInt(payload, "shopIndex"), out message),
                "sell_board" => manager.TrySellFromBoard(slot, ReadInt(payload, "boardIndex"), out message),
                "reorder_board" => manager.TryReorderBoard(slot, ReadInt(payload, "fromIndex"), ReadInt(payload, "toIndex"), out message),
                "play_hand" => manager.TryPlayFromHand(slot, ReadInt(payload, "handIndex"), out message),
                "play_hand_slot" => manager.TryPlayFromHandToSlot(slot, ReadInt(payload, "handIndex"), ReadInt(payload, "boardIndex"), out message),
                "cast_spell" => manager.TryCastSpellFromHand(slot, ReadInt(payload, "handIndex"), ReadInt(payload, "targetBoardIndex"), out message),
                "upgrade" => manager.TryUpgradeTavern(slot, out message),
                "refresh" => manager.TryRefreshShop(slot, out message),
                _ => false
            };

            if (success)
            {
                BumpVersion();
            }

            return success;
        }

        public bool TryCompleteCombat(string externalPlayerId, out string message)
        {
            message = string.Empty;
            var slot = ResolveSlot(externalPlayerId);
            if (slot < 0)
            {
                message = "Player not in match.";
                return false;
            }

            if (!manager.IsAwaitingCombatPlayback || activeCombatPlayerId != slot)
            {
                message = "No combat awaiting completion.";
                return false;
            }

            manager.CompleteHumanCombat();
            activeCombatPlayerId = null;
            BumpVersion();
            TryStartNextHumanCombat();
            return true;
        }

        public void Dispose()
        {
            manager.CombatPlaybackReady -= OnCombatPlaybackReady;
            manager.MatchEnded -= OnMatchEnded;
            manager.RecruitPhaseEnded -= OnRecruitEnded;
            GameSfxPlayer.SuppressFeedback = false;
            GameMusicPlayer.SuppressFeedback = false;
        }

        private void OnCombatPlaybackReady()
        {
            BumpVersion();
        }

        private void OnMatchEnded()
        {
            BumpVersion();
        }

        private void OnRecruitEnded()
        {
            pendingHumanCombats.Clear();
            foreach (var human in manager.Players.Where(p => p.isHuman && !p.isEliminated && p.heroHealth > 0))
            {
                pendingHumanCombats.Enqueue(human.playerId);
            }

            TryStartNextHumanCombat();
            BumpVersion();
        }

        private void TryStartNextHumanCombat()
        {
            if (manager.IsAwaitingCombatPlayback || manager.Phase != MatchPhase.Combat)
            {
                return;
            }

            while (pendingHumanCombats.Count > 0)
            {
                var nextPlayerId = pendingHumanCombats.Dequeue();
                var human = manager.GetPlayer(nextPlayerId);
                if (human == null || human.isEliminated || human.heroHealth <= 0)
                {
                    continue;
                }

                if (!manager.TryBeginHumanCombat(nextPlayerId))
                {
                    continue;
                }

                activeCombatPlayerId = nextPlayerId;
                BumpVersion();
                return;
            }

            manager.CompleteCombatPhase();
            BumpVersion();
        }

        private int ResolveSlot(string externalPlayerId)
        {
            if (string.IsNullOrEmpty(externalPlayerId))
            {
                return -1;
            }

            return playerIdToSlot.TryGetValue(externalPlayerId, out var slot) ? slot : -1;
        }

        private void BumpVersion() => stateVersion++;

        private static int ReadInt(Dictionary<string, string> payload, string key)
        {
            if (payload != null && payload.TryGetValue(key, out var raw) && int.TryParse(raw, out var value))
            {
                return value;
            }

            return 0;
        }

    }
}