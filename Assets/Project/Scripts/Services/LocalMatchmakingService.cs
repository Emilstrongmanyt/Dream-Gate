using System;
using System.Collections;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services
{
    /// <summary>
    /// Simulates ranked queue until UGS Lobby is wired. Fills empty slots with bots after a short search.
    /// </summary>
    public class LocalMatchmakingService : IMatchmakingService
    {
        private const int TargetPlayers = 8;
        private const float QueueTimeoutSeconds = 30f;

        private readonly MonoBehaviour coroutineHost;
        private Coroutine searchCoroutine;

        public bool IsSearching { get; private set; }

        public event Action<int, int> QueueUpdated;
        public event Action<MatchmakingResult> MatchFound;
        public event Action<string> QueueFailed;

        public LocalMatchmakingService(MonoBehaviour host)
        {
            coroutineHost = host;
        }

        public void StartQueue()
        {
            if (coroutineHost == null)
            {
                QueueFailed?.Invoke("Matchmaking unavailable.");
                return;
            }

            if (IsSearching)
            {
                return;
            }

            IsSearching = true;
            searchCoroutine = coroutineHost.StartCoroutine(SearchRoutine());
        }

        public void CancelQueue()
        {
            if (!IsSearching)
            {
                return;
            }

            if (coroutineHost == null)
            {
                IsSearching = false;
                searchCoroutine = null;
                QueueFailed?.Invoke("Matchmaking unavailable.");
                return;
            }

            if (searchCoroutine != null)
            {
                coroutineHost.StopCoroutine(searchCoroutine);
                searchCoroutine = null;
            }

            IsSearching = false;
        }

        private IEnumerator SearchRoutine()
        {
            if (coroutineHost == null)
            {
                IsSearching = false;
                searchCoroutine = null;
                QueueFailed?.Invoke("Matchmaking unavailable.");
                yield break;
            }

            var elapsed = 0f;
            var humansFound = 1;

            QueueUpdated?.Invoke(humansFound, TargetPlayers);

            while (elapsed < QueueTimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                var nextCount = Mathf.Clamp(1 + Mathf.FloorToInt((elapsed / QueueTimeoutSeconds) * 2f), 1, 2);
                if (nextCount != humansFound)
                {
                    humansFound = nextCount;
                    QueueUpdated?.Invoke(humansFound, TargetPlayers);
                }

                yield return null;
            }

            QueueUpdated?.Invoke(humansFound, TargetPlayers);

            var slots = new MatchSlot[TargetPlayers];
            for (var i = 0; i < TargetPlayers; i++)
            {
                slots[i] = new MatchSlot
                {
                    slotIndex = i,
                    isBot = i != 0,
                    displayName = i == 0 ? "You" : $"Bot {i + 1}"
                };
            }

            var result = new MatchmakingResult
            {
                lobbyId = $"local-{Guid.NewGuid():N}",
                matchSeed = UnityEngine.Random.Range(1, int.MaxValue),
                playersFound = humansFound,
                humanCount = humansFound,
                usedBotFill = true,
                humanSlotIndex = 0,
                slots = slots
            };

            IsSearching = false;
            searchCoroutine = null;
            MatchFound?.Invoke(result);
        }
    }
}