using System;
using System.Collections;
using System.Collections.Generic;
using DreamGate.Battlegrounds.Services;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services.Backend
{
    public class BackendMatchmakingService : IMatchmakingService
    {
        private const int TargetPlayers = 8;
        private const float PollIntervalSeconds = 1f;

        private readonly MonoBehaviour coroutineHost;
        private readonly BackendSettings settings;
        private readonly SupabaseClient supabaseClient;
        private readonly PlayerProfile profile;

        private Coroutine searchCoroutine;

        public bool IsSearching { get; private set; }

        public event Action<int, int> QueueUpdated;
        public event Action<MatchmakingResult> MatchFound;
        public event Action<string> QueueFailed;

        public BackendMatchmakingService(
            MonoBehaviour host,
            BackendSettings settings,
            SupabaseClient supabaseClient,
            PlayerProfile profile)
        {
            coroutineHost = host;
            this.settings = settings;
            this.supabaseClient = supabaseClient;
            this.profile = profile;
        }

        public void StartQueue()
        {
            if (coroutineHost == null)
            {
                QueueFailed?.Invoke("Matchmaking unavailable.");
                return;
            }

            if (!supabaseClient.IsAuthenticated || profile == null)
            {
                QueueFailed?.Invoke("Sign in to queue for rated matches.");
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

            if (searchCoroutine != null && coroutineHost != null)
            {
                coroutineHost.StopCoroutine(searchCoroutine);
                searchCoroutine = null;
            }

            IsSearching = false;
            coroutineHost?.StartCoroutine(CancelRemoteQueue());
        }

        private IEnumerator CancelRemoteQueue()
        {
            yield return supabaseClient.InvokeFunction(
                settings.matchmakingFunctionUrl,
                new Dictionary<string, object> { { "action", "cancel" } },
                (_, _, _) => { });
        }

        private IEnumerator SearchRoutine()
        {
            var joinPayload = new Dictionary<string, object>
            {
                { "action", "join" },
                { "displayName", profile.displayName },
                { "mmr", profile.mmr }
            };

            var joined = false;
            yield return supabaseClient.InvokeFunction(
                settings.matchmakingFunctionUrl,
                joinPayload,
                (success, error, _) =>
                {
                    joined = success;
                    if (!success)
                    {
                        QueueFailed?.Invoke(error);
                    }
                });

            if (!joined)
            {
                IsSearching = false;
                searchCoroutine = null;
                yield break;
            }

            QueueUpdated?.Invoke(1, TargetPlayers);

            while (IsSearching)
            {
                var status = "searching";
                var humansFound = 1;
                MatchmakingResult matchedResult = null;
                var pollError = string.Empty;

                yield return supabaseClient.InvokeFunction(
                    settings.matchmakingFunctionUrl,
                    new Dictionary<string, object> { { "action", "poll" } },
                    (success, error, response) =>
                    {
                        if (!success)
                        {
                            pollError = error;
                            return;
                        }

                        status = ApiJson.TryGetString(response, "status") ?? "searching";
                        humansFound = Math.Max(1, ApiJson.TryGetInt(response, "humansFound", 1));
                        if (status == "matched")
                        {
                            matchedResult = ParseMatchResult(response);
                        }
                    });

                if (!string.IsNullOrEmpty(pollError))
                {
                    QueueFailed?.Invoke(pollError);
                    IsSearching = false;
                    searchCoroutine = null;
                    yield break;
                }

                if (matchedResult != null)
                {
                    QueueUpdated?.Invoke(matchedResult.humanCount, TargetPlayers);
                    IsSearching = false;
                    searchCoroutine = null;
                    MatchFound?.Invoke(matchedResult);
                    yield break;
                }

                QueueUpdated?.Invoke(humansFound, TargetPlayers);
                yield return new WaitForSeconds(PollIntervalSeconds);
            }
        }

        private static MatchmakingResult ParseMatchResult(string response)
        {
            var result = new MatchmakingResult
            {
                lobbyId = ApiJson.TryGetString(response, "lobbyId"),
                matchSeed = ApiJson.TryGetInt(response, "matchSeed", UnityEngine.Random.Range(1, int.MaxValue)),
                playersFound = ApiJson.TryGetInt(response, "humansFound", 1),
                humanCount = ApiJson.TryGetInt(response, "humansFound", 1),
                usedBotFill = ApiJson.TryGetBool(response, "usedBotFill", true),
                matchServerUrl = ApiJson.TryGetString(response, "matchServerUrl"),
                humanSlotIndex = ApiJson.TryGetInt(response, "humanSlotIndex")
            };

            var slots = new List<MatchSlot>();
            var slotsIndex = response.IndexOf("\"slots\"", StringComparison.Ordinal);
            if (slotsIndex >= 0)
            {
                var arrayStart = response.IndexOf('[', slotsIndex);
                var arrayEnd = response.IndexOf(']', arrayStart);
                if (arrayStart >= 0 && arrayEnd > arrayStart)
                {
                    var arrayJson = response.Substring(arrayStart, arrayEnd - arrayStart + 1);
                    foreach (var chunk in ApiJson.ExtractObjectChunks(arrayJson))
                    {
                        slots.Add(new MatchSlot
                        {
                            slotIndex = ApiJson.TryGetInt(chunk, "slotIndex"),
                            isBot = ApiJson.TryGetBool(chunk, "isBot", true),
                            playerId = ApiJson.TryGetString(chunk, "playerId"),
                            displayName = ApiJson.TryGetString(chunk, "displayName") ?? "Player"
                        });
                    }
                }
            }

            if (slots.Count == 0)
            {
                for (var i = 0; i < TargetPlayers; i++)
                {
                    slots.Add(new MatchSlot
                    {
                        slotIndex = i,
                        isBot = i != result.humanSlotIndex,
                        displayName = i == result.humanSlotIndex ? "You" : $"Bot {i + 1}"
                    });
                }
            }

            result.slots = slots.ToArray();
            return result;
        }
    }
}