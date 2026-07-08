using System;
using System.Collections.Generic;
using System.Linq;
using DreamGate.Battlegrounds.Cards;
using DreamGate.Battlegrounds.Combat;
using DreamGate.Battlegrounds.Economy;
using DreamGate.Battlegrounds.Heroes;
using DreamGate.Battlegrounds.Players;
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

        public const int StartingHeroHealth = 40;
        public const int StartingGoldTurnOne = 3;
        public const int GoldPerTurnIncrement = 1;
        public const int MaxGold = 10;

        public const int MinionCost = 3;
        public const int SellValue = 1;
        public const int TavernUpgradeCost = 4;
        public const int ShopRefreshCost = 1;
        public const int TripleGoldReward = 3;

        public const int BaseRecruitSeconds = 20;
        public const int RecruitSecondsPerTurn = 5;
        public const int MaxRecruitSeconds = 80;

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

        public void Initialize(int humanId = 0)
        {
            Initialize(MatchMode.Practice, humanId, -1);
        }

        public void Initialize(MatchMode mode, int humanId = 0, int matchSeed = -1)
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
                var isHuman = i == humanId;
                var player = new PlayerState
                {
                    playerId = i,
                    displayName = isHuman ? "You" : mode == MatchMode.Rated ? $"Player {i + 1}" : $"Bot {i}",
                    heroId = $"hero_{i}",
                    heroName = HeroRegistry.GetHeroName(i),
                    isHuman = isHuman,
                    heroHealth = MatchConfig.StartingHeroHealth,
                    tavernTier = MatchConfig.StartingTavernTier
                };
                players.Add(player);

                if (isHuman)
                {
                    humanPlayer = player;
                }
            }

            BeginRecruitPhase();
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
            StateChanged?.Invoke();

            if (RecruitTimeRemaining <= 0f)
            {
                EndRecruitPhase();
            }
        }

        public bool TryBuyFromShop(int shopIndex, out string message)
        {
            EnsureRecruitPhase();
            var success = ShopSystem.TryBuy(humanPlayer, shopIndex, out message);
            if (success)
            {
                Post(message);
                StateChanged?.Invoke();
                GameMusicPlayer.UpdateMatchMusic(GetAlivePlayerCount());
            }

            return success;
        }

        public bool TrySellFromBoard(int boardIndex, out string message)
        {
            EnsureRecruitPhase();
            var success = ShopSystem.TrySell(humanPlayer, boardIndex, out message);
            if (success)
            {
                Post(message);
                StateChanged?.Invoke();
            }

            return success;
        }

        public bool TryReorderBoard(int fromIndex, int toIndex, out string message)
        {
            EnsureRecruitPhase();
            var success = ShopSystem.TryReorderBoard(humanPlayer, fromIndex, toIndex, out message);
            if (success && fromIndex != toIndex)
            {
                Post(message);
                StateChanged?.Invoke();
            }

            return success;
        }

        public bool TryPlayFromHand(int handIndex, out string message)
        {
            EnsureRecruitPhase();
            var success = ShopSystem.TryPlayFromHand(humanPlayer, handIndex, out message);
            if (success)
            {
                Post(message);
                StateChanged?.Invoke();
            }

            return success;
        }

        public bool TryPlayFromHandToSlot(int handIndex, int boardIndex, out string message)
        {
            EnsureRecruitPhase();
            var success = ShopSystem.TryPlayFromHandToSlot(humanPlayer, handIndex, boardIndex, out message);
            if (success)
            {
                Post(message);
                StateChanged?.Invoke();
            }

            return success;
        }

        public bool TryCastSpellFromHand(int handIndex, int targetBoardIndex, out string message)
        {
            EnsureRecruitPhase();
            var success = SpellSystem.TryCast(humanPlayer, handIndex, targetBoardIndex, out message);
            if (success)
            {
                GameSfxPlayer.PlayRecruit(humanPlayer, GameSfxPlayer.PlayDropCard);
                Post(message);
                StateChanged?.Invoke();
            }

            return success;
        }

        public bool TryUpgradeTavern(out string message)
        {
            EnsureRecruitPhase();
            var success = ShopSystem.TryUpgradeTavern(humanPlayer, out message);
            if (success)
            {
                Post(message);
                StateChanged?.Invoke();
            }

            return success;
        }

        public bool TryRefreshShop(out string message)
        {
            EnsureRecruitPhase();
            var success = ShopSystem.TryRefreshShop(humanPlayer, out message);
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

            ApplyCombatDamage(humanPlayer, PendingOpponent, PendingHumanCombat);

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
            Phase = MatchPhase.DamageResolution;
            StateChanged?.Invoke();
            AdvanceTurnOrEndMatch();
        }

        private void BeginRecruitPhase()
        {
            Phase = MatchPhase.Recruit;
            RecruitTimeRemaining = MatchConfig.GetRecruitDurationForTurn(Turn);
            humanPlayer.gold = MatchConfig.GetGoldIncomeForTurn(Turn);
            ShopSystem.RefreshShop(humanPlayer, matchRandom.Next());

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

            if (humanPlayer.isEliminated || humanPlayer.heroHealth <= 0)
            {
                AdvanceTurnOrEndMatch();
                return;
            }

            var opponent = PickCombatOpponent();
            if (opponent == null)
            {
                AdvanceTurnOrEndMatch();
                return;
            }

            PendingOpponent = opponent;
            PendingHumanCombat = CombatSimulator.Simulate(humanPlayer, opponent);
            Post($"Your combat vs {opponent.heroName} ({opponent.displayName}).");
            CombatPlaybackReady?.Invoke();
            StateChanged?.Invoke();
        }

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

        private PlayerState PickCombatOpponent()
        {
            var candidates = players
                .Where(p => !p.isHuman && !p.isEliminated && p.heroHealth > 0)
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