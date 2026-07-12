using System;
using System.Collections.Generic;
using System.Linq;
using DreamGate.Battlegrounds.Cards;
using DreamGate.Battlegrounds.Combat;
using DreamGate.Battlegrounds.Economy;
using DreamGate.Battlegrounds.Heroes;
using DreamGate.Battlegrounds.Players;
using DreamGate.Battlegrounds.Networking;
using DreamGate.Battlegrounds.Services;
using UnityEngine;

namespace DreamGate.Battlegrounds.Core
{
    public enum MatchPhase
    {
        Recruit,
        Combat,
        DamageResolution,
        GameOver
    }

    public static class MatchConfig
    {
        public const int MaxPlayers = 8;
        public const int BoardSize = 5;
        public const int MaxHandSize = 6;
        public const int ShopSlotCount = 5;
        public const int MaxTavernTier = 4;

        public const int StartingHeroHealth = 30;
        public const int StartingGoldTurnOne = 3;
        public const int GoldPerTurnIncrement = 1;
        public const int MaxGold = 10;

        public const int MinionCost = 3;
        public const int SellValue = 1;
        public static int GetTavernUpgradeCost(int currentTier) => 2 * (currentTier + 1);
        public const int ShopRefreshCost = 1;
        public const int TripleGoldReward = 3;

        public const int BaseRecruitSeconds = 20;
        public const int RecruitSecondsPerTurn = 5;
        public const int MaxRecruitSeconds = 60;

        public const int StartingTavernTier = 1;

        public static int GetGoldIncomeForTurn(int turn)
        {
            return Mathf.Min(MaxGold, StartingGoldTurnOne + (turn - 1) * GoldPerTurnIncrement);
        }

        public static int GetRecruitDurationForTurn(int turn)
        {
            return Mathf.Min(MaxRecruitSeconds, BaseRecruitSeconds + (turn - 1) * RecruitSecondsPerTurn);
        }
    }

    public class MatchManager
    {
        public event Action StateChanged;
        public event Action<string> MessagePosted;
        public event Action MatchEnded;
        public event Action CombatPlaybackReady;
        public event Action RecruitPhaseEnded;

        public bool AuthoritativeSimulation { get; set; }
        public IMatchActionRelay ActionRelay { get; set; }

        public MatchMode Mode { get; private set; } = MatchMode.Practice;
        public int Turn { get; private set; } = 1;
        public MatchPhase Phase { get; private set; } = MatchPhase.Recruit;
        public float RecruitTimeRemaining { get; private set; }
        public IReadOnlyList<PlayerState> Players => players;
        public MatchResult FinalResult { get; private set; }
        public CombatResult PendingHumanCombat { get; private set; }
        public PlayerState PendingOpponent { get; private set; }
        public bool IsAwaitingCombatPlayback => Phase == MatchPhase.Combat && PendingHumanCombat != null;

        private readonly List<PlayerState> players = new();
        private readonly List<string> eliminationOrder = new();
        private System.Random matchRandom = new();
        private PlayerState humanPlayer;
        private int humanPlayerId;
        private int lastCombatOpponentId = -1;
        private int nextPlacement = MatchConfig.MaxPlayers;
        private int lastRecruitTimerDisplay = -1;
        private int pendingHumanPlayerId = -1;

        public void Initialize(int humanId = 0)
        {
            Initialize(MatchMode.Practice, humanId, -1);
        }

        public void Initialize(MatchMode mode, int humanId = 0, int matchSeed = -1, MatchSlot[] slots = null)
        {
            CardRegistry.Initialize();
            players.Clear();
            eliminationOrder.Clear();
            Turn = 1;
            Phase = MatchPhase.Recruit;
            Mode = mode;
            humanPlayerId = humanId;
            lastCombatOpponentId = -1;
            nextPlacement = MatchConfig.MaxPlayers;
            FinalResult = null;
            PendingHumanCombat = null;
            PendingOpponent = null;
            matchRandom = matchSeed >= 0 ? new System.Random(matchSeed) : new System.Random();

            for (var i = 0; i < MatchConfig.MaxPlayers; i++)
            {
                var slot = slots != null && i < slots.Length ? slots[i] : null;
                var isHuman = slot != null ? !slot.isBot : i == humanId;
                var isLocalHuman = i == humanId;
                var displayName = ResolveDisplayName(mode, i, isLocalHuman, slot);
                var player = new PlayerState
                {
                    playerId = i,
                    displayName = displayName,
                    heroName = HeroRegistry.GetHeroName(i),
                    isHuman = isHuman,
                    heroHealth = MatchConfig.StartingHeroHealth,
                    tavernTier = MatchConfig.StartingTavernTier
                };

                if (!isHuman)
                {
                    HeroRegistry.AssignRandomBotPortrait(player, matchRandom);
                }
                else
                {
                    player.heroId = $"hero_{i}";
                }

                players.Add(player);

                if (isLocalHuman)
                {
                    humanPlayer = player;
                }
                else if (humanPlayer == null && isHuman)
                {
                    humanPlayer = player;
                }
            }

            BeginRecruitPhase();
        }

        public PlayerState GetPlayer(int playerId) =>
            players.FirstOrDefault(p => p.playerId == playerId);

        private static string ResolveDisplayName(MatchMode mode, int slotIndex, bool isHuman, MatchSlot slot)
        {
            if (isHuman)
            {
                return "You";
            }

            if (slot != null && !string.IsNullOrWhiteSpace(slot.displayName))
            {
                return slot.displayName;
            }

            return mode == MatchMode.Rated ? $"Player {slotIndex + 1}" : $"Bot {slotIndex}";
        }

        public PlayerState GetHumanPlayer() => humanPlayer;

        public int GetAlivePlayerCount()
        {
            return players.Count(p => !p.isEliminated && p.heroHealth > 0);
        }

        public string GetLeaderboardSummary()
        {
            var alive = players
                .Where(p => !p.isEliminated && p.heroHealth > 0)
                .OrderByDescending(p => p.isHuman)
                .ThenByDescending(p => p.heroHealth)
                .Select(p => $"{p.heroName} {p.heroHealth}hp");
            return $"Alive ({GetAlivePlayerCount()}): " + string.Join(", ", alive);
        }

        public void TickRecruitTimer(float deltaTime)
        {
            if (Phase != MatchPhase.Recruit)
            {
                return;
            }

            RecruitTimeRemaining -= deltaTime;
            var timerDisplay = Mathf.CeilToInt(RecruitTimeRemaining);
            if (timerDisplay != lastRecruitTimerDisplay)
            {
                lastRecruitTimerDisplay = timerDisplay;
                StateChanged?.Invoke();
            }

            if (RecruitTimeRemaining <= 0f)
            {
                EndRecruitPhase();
            }
        }

        public void TickRecruitTimerDisplayOnly(float deltaTime)
        {
            if (Phase != MatchPhase.Recruit)
            {
                return;
            }

            RecruitTimeRemaining = Mathf.Max(0f, RecruitTimeRemaining - deltaTime);
            var timerDisplay = Mathf.CeilToInt(RecruitTimeRemaining);
            if (timerDisplay != lastRecruitTimerDisplay)
            {
                lastRecruitTimerDisplay = timerDisplay;
                StateChanged?.Invoke();
            }
        }

        public void SyncRecruitTimerFromServer(float serverTimeRemaining)
        {
            if (Phase != MatchPhase.Recruit)
            {
                return;
            }

            var snapped = Mathf.Max(0f, serverTimeRemaining);
            if (Math.Abs(RecruitTimeRemaining - snapped) < 0.01f)
            {
                return;
            }

            RecruitTimeRemaining = snapped;
            var timerDisplay = Mathf.CeilToInt(RecruitTimeRemaining);
            if (timerDisplay != lastRecruitTimerDisplay)
            {
                lastRecruitTimerDisplay = timerDisplay;
                StateChanged?.Invoke();
            }
        }

        public bool TryBuyFromShop(int shopIndex, out string message) =>
            TryBuyFromShop(humanPlayerId, shopIndex, out message);

        public bool TryBuyFromShop(int playerId, int shopIndex, out string message)
        {
            if (TryRelayRecruitAction("buy_shop", playerId, new Dictionary<string, int> { { "shopIndex", shopIndex } }, out message))
            {
                return true;
            }

            if (ActionRelay != null && ActionRelay.IsAuthoritative)
            {
                return false;
            }

            EnsureRecruitPhase();
            var player = RequirePlayer(playerId);
            var success = ShopSystem.TryBuy(player, shopIndex, out message);
            if (success)
            {
                Post(message);
                StateChanged?.Invoke();
                GameMusicPlayer.UpdateMatchMusic(GetAlivePlayerCount());
            }

            return success;
        }

        public bool TrySellFromBoard(int boardIndex, out string message) =>
            TrySellFromBoard(humanPlayerId, boardIndex, out message);

        public bool TrySellFromBoard(int playerId, int boardIndex, out string message)
        {
            if (TryRelayRecruitAction("sell_board", playerId, new Dictionary<string, int> { { "boardIndex", boardIndex } }, out message))
            {
                return true;
            }

            if (ActionRelay != null && ActionRelay.IsAuthoritative)
            {
                return false;
            }

            EnsureRecruitPhase();
            var player = RequirePlayer(playerId);
            var success = ShopSystem.TrySell(player, boardIndex, out message);
            if (success)
            {
                Post(message);
                StateChanged?.Invoke();
            }

            return success;
        }

        public bool TryReorderBoard(int fromIndex, int toIndex, out string message) =>
            TryReorderBoard(humanPlayerId, fromIndex, toIndex, out message);

        public bool TryReorderBoard(int playerId, int fromIndex, int toIndex, out string message)
        {
            if (TryRelayRecruitAction(
                    "reorder_board",
                    playerId,
                    new Dictionary<string, int> { { "fromIndex", fromIndex }, { "toIndex", toIndex } },
                    out message))
            {
                return true;
            }

            if (ActionRelay != null && ActionRelay.IsAuthoritative)
            {
                return false;
            }

            EnsureRecruitPhase();
            var player = RequirePlayer(playerId);
            var success = ShopSystem.TryReorderBoard(player, fromIndex, toIndex, out message);
            if (success && fromIndex != toIndex)
            {
                Post(message);
                StateChanged?.Invoke();
            }

            return success;
        }

        public bool TryPlayFromHand(int handIndex, out string message) =>
            TryPlayFromHand(humanPlayerId, handIndex, out message);

        public bool TryPlayFromHand(int playerId, int handIndex, out string message)
        {
            if (TryRelayRecruitAction("play_hand", playerId, new Dictionary<string, int> { { "handIndex", handIndex } }, out message))
            {
                return true;
            }

            if (ActionRelay != null && ActionRelay.IsAuthoritative)
            {
                return false;
            }

            EnsureRecruitPhase();
            var player = RequirePlayer(playerId);
            var success = ShopSystem.TryPlayFromHand(player, handIndex, out message);
            if (success)
            {
                Post(message);
                StateChanged?.Invoke();
            }

            return success;
        }

        public bool TryPlayFromHandToSlot(int handIndex, int boardIndex, out string message) =>
            TryPlayFromHandToSlot(humanPlayerId, handIndex, boardIndex, out message);

        public bool TryPlayFromHandToSlot(int playerId, int handIndex, int boardIndex, out string message)
        {
            if (TryRelayRecruitAction(
                    "play_hand_slot",
                    playerId,
                    new Dictionary<string, int> { { "handIndex", handIndex }, { "boardIndex", boardIndex } },
                    out message))
            {
                return true;
            }

            if (ActionRelay != null && ActionRelay.IsAuthoritative)
            {
                return false;
            }

            EnsureRecruitPhase();
            var player = RequirePlayer(playerId);
            var success = ShopSystem.TryPlayFromHandToSlot(player, handIndex, boardIndex, out message);
            if (success)
            {
                Post(message);
                StateChanged?.Invoke();
            }

            return success;
        }

        public bool TryCastSpellFromHand(int handIndex, int targetBoardIndex, out string message) =>
            TryCastSpellFromHand(humanPlayerId, handIndex, targetBoardIndex, out message);

        public bool TryCastSpellFromHand(int playerId, int handIndex, int targetBoardIndex, out string message)
        {
            if (TryRelayRecruitAction(
                    "cast_spell",
                    playerId,
                    new Dictionary<string, int> { { "handIndex", handIndex }, { "targetBoardIndex", targetBoardIndex } },
                    out message))
            {
                return true;
            }

            if (ActionRelay != null && ActionRelay.IsAuthoritative)
            {
                return false;
            }

            EnsureRecruitPhase();
            var player = RequirePlayer(playerId);
            var success = SpellSystem.TryCast(player, handIndex, targetBoardIndex, out message);
            if (success)
            {
                GameSfxPlayer.PlayRecruit(player, GameSfxPlayer.PlayDropCard);
                Post(message);
                StateChanged?.Invoke();
            }

            return success;
        }

        public bool TryUpgradeTavern(out string message) =>
            TryUpgradeTavern(humanPlayerId, out message);

        public bool TryUpgradeTavern(int playerId, out string message)
        {
            if (TryRelayRecruitAction("upgrade", playerId, new Dictionary<string, int>(), out message))
            {
                return true;
            }

            if (ActionRelay != null && ActionRelay.IsAuthoritative)
            {
                return false;
            }

            EnsureRecruitPhase();
            var player = RequirePlayer(playerId);
            var success = ShopSystem.TryUpgradeTavern(player, out message);
            if (success)
            {
                Post(message);
                StateChanged?.Invoke();
            }

            return success;
        }

        public bool TryRefreshShop(out string message) =>
            TryRefreshShop(humanPlayerId, out message);

        public bool TryRefreshShop(int playerId, out string message)
        {
            if (TryRelayRecruitAction("refresh", playerId, new Dictionary<string, int>(), out message))
            {
                return true;
            }

            if (ActionRelay != null && ActionRelay.IsAuthoritative)
            {
                return false;
            }

            EnsureRecruitPhase();
            var player = RequirePlayer(playerId);
            var success = ShopSystem.TryRefreshShop(player, out message);
            if (success)
            {
                Post(message);
                StateChanged?.Invoke();
            }

            return success;
        }

        public void EndRecruitEarly()
        {
            if (Phase == MatchPhase.Recruit)
            {
                EndRecruitPhase();
            }
        }

        public void CompleteHumanCombat()
        {
            if (!IsAwaitingCombatPlayback)
            {
                return;
            }

            if (ActionRelay != null && ActionRelay.IsAuthoritative)
            {
                ActionRelay.TryRelayCompleteCombat(out _);
                return;
            }

            var actingHuman = GetPlayer(pendingHumanPlayerId) ?? humanPlayer;
            ApplyCombatDamage(actingHuman, PendingOpponent, PendingHumanCombat);

            if (PendingHumanCombat.outcome == CombatOutcome.AttackerWins)
            {
                Post($"You win! {PendingOpponent.heroName} takes {PendingHumanCombat.damageToDefender} damage.");
            }
            else if (PendingHumanCombat.outcome == CombatOutcome.DefenderWins)
            {
                Post($"You lose. You take {PendingHumanCombat.damageToAttacker} damage.");
            }
            else
            {
                Post("Combat drawn. No damage dealt.");
            }

            foreach (var line in PendingHumanCombat.combatLog)
            {
                Post(line);
            }

            PendingHumanCombat = null;
            PendingOpponent = null;
            pendingHumanPlayerId = -1;
            if (AuthoritativeSimulation)
            {
                StateChanged?.Invoke();
                return;
            }

            Phase = MatchPhase.DamageResolution;
            StateChanged?.Invoke();
            AdvanceTurnOrEndMatch();
        }

        private void BeginRecruitPhase()
        {
            Phase = MatchPhase.Recruit;
            RecruitTimeRemaining = MatchConfig.GetRecruitDurationForTurn(Turn);
            lastRecruitTimerDisplay = Mathf.CeilToInt(RecruitTimeRemaining);

            foreach (var human in players.Where(p => p.isHuman && !p.isEliminated))
            {
                human.gold = MatchConfig.GetGoldIncomeForTurn(Turn);
                ShopSystem.RefreshShop(human, matchRandom.Next());
            }

            foreach (var bot in players.Where(p => !p.isHuman && !p.isEliminated))
            {
                BotPlayerController.TakeRecruitTurn(bot, Turn, matchRandom.Next());
            }

            Post($"Turn {Turn} | {humanPlayer.heroName} | Gold: {humanPlayer.gold} | Tavern {humanPlayer.tavernTier} | Timer: {Mathf.CeilToInt(RecruitTimeRemaining)}s");
            Post(GetLeaderboardSummary());
            GameMusicPlayer.UpdateMatchMusic(GetAlivePlayerCount());
            StateChanged?.Invoke();
        }

        private void EndRecruitPhase()
        {
            Phase = MatchPhase.Combat;
            ResolveBotOnlyCombats();

            if (AuthoritativeSimulation)
            {
                RecruitPhaseEnded?.Invoke();
                StateChanged?.Invoke();
                return;
            }

            if (humanPlayer.isEliminated || humanPlayer.heroHealth <= 0)
            {
                AdvanceTurnOrEndMatch();
                return;
            }

            if (!TryBeginHumanCombat(humanPlayerId))
            {
                AdvanceTurnOrEndMatch();
            }
        }

        public bool TryBeginHumanCombat(int playerId)
        {
            var human = GetPlayer(playerId);
            if (human == null || human.isEliminated || human.heroHealth <= 0)
            {
                return false;
            }

            var opponent = PickCombatOpponent(playerId);
            if (opponent == null)
            {
                return false;
            }

            pendingHumanPlayerId = playerId;
            PendingOpponent = opponent;
            PendingHumanCombat = CombatSimulator.Simulate(human, opponent);
            Post($"Combat: {human.displayName} vs {opponent.heroName} ({opponent.displayName}).");
            CombatPlaybackReady?.Invoke();
            StateChanged?.Invoke();
            return true;
        }

        public void CompleteCombatPhase()
        {
            if (Phase != MatchPhase.Combat || IsAwaitingCombatPlayback)
            {
                return;
            }

            Phase = MatchPhase.DamageResolution;
            StateChanged?.Invoke();
            AdvanceTurnOrEndMatch();
        }

        // Background bot-vs-bot fights: resolve silently (no UI playback, no combat SFX).
        private void ResolveBotOnlyCombats()
        {
            var bots = players
                .Where(p => !p.isHuman && !p.isEliminated && p.heroHealth > 0)
                .OrderBy(_ => matchRandom.Next())
                .ToList();

            while (bots.Count >= 2)
            {
                var attacker = bots[0];
                var defender = bots[1];
                bots.RemoveRange(0, 2);

                var combat = CombatSimulator.Simulate(attacker, defender);
                ApplyCombatDamage(attacker, defender, combat);
                Post($"{attacker.displayName} vs {defender.displayName}: {combat.outcome}");
            }
        }

        private void ApplyCombatDamage(PlayerState attacker, PlayerState defender, CombatResult combat)
        {
            if (combat.outcome == CombatOutcome.AttackerWins)
            {
                combat.damageToDefender = DamageCalculator.CalculateSurvivorTierSum(combat.attackerSnapshot.GetLivingBoard());
                defender.heroHealth -= combat.damageToDefender;
                defender.damageTaken += combat.damageToDefender;
                attacker.damageDealt += combat.damageToDefender;

                if (defender.heroHealth <= 0)
                {
                    EliminatePlayer(defender);
                }
            }
            else if (combat.outcome == CombatOutcome.DefenderWins)
            {
                combat.damageToAttacker = DamageCalculator.CalculateSurvivorTierSum(combat.defenderSnapshot.GetLivingBoard());
                attacker.heroHealth -= combat.damageToAttacker;
                attacker.damageTaken += combat.damageToAttacker;
                defender.damageDealt += combat.damageToAttacker;

                if (attacker.heroHealth <= 0)
                {
                    EliminatePlayer(attacker);
                }
            }
        }

        private void EliminatePlayer(PlayerState player)
        {
            if (player.isEliminated)
            {
                return;
            }

            player.isEliminated = true;
            player.heroHealth = 0;
            player.placement = nextPlacement--;
            eliminationOrder.Add(player.displayName);
            Post($"{player.heroName} eliminated! Place: #{player.placement}");
            if (player.isHuman)
            {
                GameSfxPlayer.PlayFailed();
            }

            GameMusicPlayer.UpdateMatchMusic(GetAlivePlayerCount());
        }

        private void AdvanceTurnOrEndMatch()
        {
            if (AuthoritativeSimulation)
            {
                if (GetAlivePlayerCount() <= 1)
                {
                    foreach (var survivor in players.Where(p => !p.isEliminated && p.heroHealth > 0))
                    {
                        survivor.placement = 1;
                    }

                    Phase = MatchPhase.GameOver;
                    MatchEnded?.Invoke();
                    StateChanged?.Invoke();
                    return;
                }

                Turn++;
                BeginRecruitPhase();
                return;
            }

            if (humanPlayer.isEliminated || humanPlayer.heroHealth <= 0)
            {
                EliminatePlayer(humanPlayer);
                EndMatch(false);
                return;
            }

            if (GetAlivePlayerCount() <= 1)
            {
                humanPlayer.placement = 1;
                EndMatch(true);
                return;
            }

            Turn++;
            BeginRecruitPhase();
        }

        private void EndMatch(bool playerWon)
        {
            Phase = MatchPhase.GameOver;
            FinalResult = new MatchResult
            {
                matchMode = Mode,
                playerWon = playerWon,
                placement = humanPlayer.placement > 0 ? humanPlayer.placement : 1,
                turnsPlayed = Turn,
                finalHeroHealth = humanPlayer.heroHealth,
                damageDealt = humanPlayer.damageDealt,
                damageTaken = humanPlayer.damageTaken,
                heroName = humanPlayer.heroName
            };
            FinalResult.eliminationOrder.AddRange(eliminationOrder);

            Post(playerWon
                ? $"Victory! {humanPlayer.heroName} wins!"
                : $"Defeat. {humanPlayer.heroName} finished #{humanPlayer.placement}.");
            if (playerWon)
            {
                GameSfxPlayer.PlayVictory();
                GameMusicPlayer.UpdateMatchMusic(GetAlivePlayerCount(), playerWon: true);
            }
            else
            {
                GameSfxPlayer.PlayFailed();
            }

            MatchEnded?.Invoke();
            StateChanged?.Invoke();
        }

        private PlayerState PickCombatOpponent(int forPlayerId)
        {
            var candidates = players
                .Where(p => p.playerId != forPlayerId && !p.isEliminated && p.heroHealth > 0)
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            if (lastCombatOpponentId >= 0)
            {
                var filtered = candidates.Where(p => p.playerId != lastCombatOpponentId).ToList();
                if (filtered.Count > 0)
                {
                    candidates = filtered;
                }
            }

            var opponent = candidates[matchRandom.Next(candidates.Count)];
            lastCombatOpponentId = opponent.playerId;
            return opponent;
        }

        public void BeginCombatPlaybackFromSnapshot(CombatSnapshot combatSnapshot, int localSlotIndex)
        {
            if (combatSnapshot == null)
            {
                return;
            }

            pendingHumanPlayerId = localSlotIndex;
            PendingOpponent = GetPlayer(combatSnapshot.opponentPlayerId) ?? new PlayerState
            {
                playerId = combatSnapshot.opponentPlayerId,
                displayName = combatSnapshot.opponentDisplayName,
                heroName = combatSnapshot.opponentHeroName
            };

            PendingHumanCombat = new CombatResult
            {
                outcome = (CombatOutcome)combatSnapshot.outcome,
                damageToDefender = combatSnapshot.damageToDefender,
                damageToAttacker = combatSnapshot.damageToAttacker,
                combatEvents = combatSnapshot.events?.Select(e => new CombatEvent
                {
                    type = (CombatEventType)e.type,
                    attackerBoardIndex = e.attackerSlot,
                    defenderBoardIndex = e.defenderSlot,
                    isRecoil = e.isRecoil,
                    damageAmount = e.damage
                }).ToList() ?? new List<CombatEvent>()
            };

            Phase = MatchPhase.Combat;
            CombatPlaybackReady?.Invoke();
            StateChanged?.Invoke();
        }

        public void ApplySnapshot(MatchSnapshot snapshot, int localSlotIndex)
        {
            if (snapshot == null)
            {
                return;
            }

            humanPlayerId = localSlotIndex;
            Turn = snapshot.turn;
            Phase = (MatchPhase)snapshot.phase;
            RecruitTimeRemaining = snapshot.recruitTimeRemaining;
            lastRecruitTimerDisplay = Mathf.CeilToInt(RecruitTimeRemaining);

            foreach (var playerSnapshot in snapshot.players)
            {
                var player = GetPlayer(playerSnapshot.playerId);
                if (player == null)
                {
                    continue;
                }

                player.displayName = playerSnapshot.displayName;
                player.heroId = playerSnapshot.heroId;
                player.heroName = playerSnapshot.heroName;
                player.isHuman = playerSnapshot.playerId == localSlotIndex;
                player.isEliminated = playerSnapshot.isEliminated;
                player.placement = playerSnapshot.placement;
                player.heroHealth = playerSnapshot.heroHealth;
                player.damageDealt = playerSnapshot.damageDealt;
                player.damageTaken = playerSnapshot.damageTaken;
                player.gold = playerSnapshot.gold;
                player.tavernTier = playerSnapshot.tavernTier;
                player.doomNextCombat = playerSnapshot.doomNextCombat;
                player.board = playerSnapshot.board ?? new MinionInstance[MatchConfig.BoardSize];
                player.hand = playerSnapshot.hand?.ToList() ?? new List<MinionInstance>();
                player.shopCardIds = playerSnapshot.shopCardIds?.ToList() ?? new List<string>();

                if (player.playerId == localSlotIndex)
                {
                    humanPlayer = player;
                }
            }

            if (snapshot.matchEnded && snapshot.matchEnd != null)
            {
                Phase = MatchPhase.GameOver;
                FinalResult = new MatchResult
                {
                    matchMode = Mode,
                    playerWon = snapshot.matchEnd.playerWon,
                    placement = snapshot.matchEnd.placement,
                    turnsPlayed = snapshot.matchEnd.turnsPlayed,
                    finalHeroHealth = snapshot.matchEnd.finalHeroHealth,
                    damageDealt = snapshot.matchEnd.damageDealt,
                    damageTaken = snapshot.matchEnd.damageTaken,
                    heroName = snapshot.matchEnd.heroName
                };
                MatchEnded?.Invoke();
            }

            StateChanged?.Invoke();
        }

        private bool TryRelayRecruitAction(
            string action,
            int playerId,
            Dictionary<string, int> payload,
            out string message)
        {
            message = string.Empty;
            if (ActionRelay == null || !ActionRelay.IsAuthoritative)
            {
                return false;
            }

            if (Phase != MatchPhase.Recruit)
            {
                message = "Recruit phase has ended.";
                return false;
            }

            return ActionRelay.TryRelayAction(action, playerId, payload, out message);
        }

        private PlayerState RequirePlayer(int playerId)
        {
            var player = GetPlayer(playerId);
            if (player == null)
            {
                throw new InvalidOperationException($"Player slot {playerId} not found.");
            }

            return player;
        }

        private void EnsureRecruitPhase()
        {
            if (Phase != MatchPhase.Recruit)
            {
                throw new InvalidOperationException("Action only available during recruit phase.");
            }
        }

        private void Post(string message)
        {
            MessagePosted?.Invoke(message);
        }
    }
}