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
        private const float MinSearchSeconds = 2f;
        private const float MaxSearchSeconds = 6f;

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

            if (searchCoroutine != null)
            {
                coroutineHost.StopCoroutine(searchCoroutine);
                searchCoroutine = null;
            }

            IsSearching = false;
        }

        private IEnumerator SearchRoutine()
        {
            var searchDuration = UnityEngine.Random.Range(MinSearchSeconds, MaxSearchSeconds);
            var elapsed = 0f;
            var playersFound = 1;

            QueueUpdated?.Invoke(playersFound, TargetPlayers);

            while (elapsed < searchDuration)
            {
                elapsed += Time.deltaTime;
                var nextCount = Mathf.Clamp(1 + Mathf.FloorToInt((elapsed / searchDuration) * (TargetPlayers - 1)), 1, TargetPlayers - 1);
                if (nextCount != playersFound)
                {
                    playersFound = nextCount;
                    QueueUpdated?.Invoke(playersFound, TargetPlayers);
                }

                yield return null;
            }

            playersFound = TargetPlayers;
            QueueUpdated?.Invoke(playersFound, TargetPlayers);

            var result = new MatchmakingResult
            {
                lobbyId = $"local-{Guid.NewGuid():N}",
                matchSeed = UnityEngine.Random.Range(1, int.MaxValue),
                playersFound = playersFound,
                usedBotFill = true
            };

            IsSearching = false;
            searchCoroutine = null;
            MatchFound?.Invoke(result);
        }
    }
}