using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DreamGate.Battlegrounds.Combat;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Players;
using DreamGate.Battlegrounds.Services;
using DreamGate.Battlegrounds.Services.Backend;
using UnityEngine;
using UnityEngine.Networking;

namespace DreamGate.Battlegrounds.Networking
{
    public class RemoteMatchClient : INetworkMatchHost, IMatchActionRelay
    {
        private const float PollIntervalSeconds = 0.25f;

        private readonly string serverUrl;
        private readonly string lobbyId;
        private readonly int localSlotIndex;
        private readonly MatchSlot[] slots;
        private readonly int matchSeed;

        private MatchManager matchManager;
        private string playerId;
        private int lastSnapshotVersion = -1;
        private bool connected;
        private Coroutine pollCoroutine;

        public bool IsServer => false;
        public bool IsAuthoritative => connected;
        public MatchMode Mode => MatchMode.Rated;

        public event Action CombatPlaybackRequested;

        public RemoteMatchClient(string serverUrl, string lobbyId, int localSlotIndex, MatchSlot[] slots, int matchSeed)
        {
            this.serverUrl = serverUrl?.TrimEnd('/');
            this.lobbyId = lobbyId;
            this.localSlotIndex = localSlotIndex;
            this.slots = slots ?? Array.Empty<MatchSlot>();
            this.matchSeed = matchSeed;
        }

        public void InitializeMatch(MatchManager manager)
        {
            matchManager = manager;
            matchManager.ActionRelay = this;
            playerId = DreamGateServices.Profile?.playerId ?? string.Empty;
            CloudCoroutineHost.Instance.Run(ConnectRoutine());
        }

        public void TickRecruitTimer(float deltaTime)
        {
        }

        public void Dispose()
        {
            if (pollCoroutine != null)
            {
                CloudCoroutineHost.Instance.StopCoroutine(pollCoroutine);
                pollCoroutine = null;
            }

            if (matchManager != null)
            {
                matchManager.ActionRelay = null;
            }

            matchManager = null;
            connected = false;
        }

        public bool TryRelayAction(string action, int playerId, Dictionary<string, int> payload, out string message)
        {
            message = string.Empty;
            if (!connected)
            {
                message = "Not connected to match server.";
                return false;
            }

            CloudCoroutineHost.Instance.Run(SendActionRoutine(action, payload));
            return true;
        }

        public bool TryRelayCompleteCombat(out string message)
        {
            message = string.Empty;
            if (!connected)
            {
                message = "Not connected to match server.";
                return false;
            }

            CloudCoroutineHost.Instance.Run(SendCompleteCombatRoutine());
            return true;
        }

        private IEnumerator ConnectRoutine()
        {
            if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(lobbyId))
            {
                Debug.LogWarning("RemoteMatchClient missing server URL or lobby id.");
                yield break;
            }

            var slotsJson = BuildSlotsJson(slots);
            var body =
                "{" +
                $"\"lobbyId\":\"{ApiJson.Escape(lobbyId)}\"," +
                $"\"playerId\":\"{ApiJson.Escape(playerId)}\"," +
                $"\"displayName\":\"{ApiJson.Escape(DreamGateServices.Profile?.displayName ?? "Dreamer")}\"," +
                $"\"matchSeed\":{matchSeed}," +
                $"\"slots\":{slotsJson}" +
                "}";

            var httpUrl = ToHttpUrl(serverUrl);
            using var request = CreatePost($"{httpUrl}/match/join", body);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"RemoteMatchClient join failed: {request.error}");
                yield break;
            }

            var response = request.downloadHandler?.text ?? string.Empty;
            if (!ApiJson.TryGetBool(response, "authoritative", false))
            {
                Debug.LogWarning("Match server did not return authoritative session.");
                yield break;
            }

            connected = true;
            ApplyResponseSnapshot(response);
            pollCoroutine = CloudCoroutineHost.Instance.Run(PollRoutine());
            Debug.Log($"RemoteMatchClient connected to authoritative match {lobbyId}.");
        }

        private IEnumerator PollRoutine()
        {
            var wait = new WaitForSeconds(PollIntervalSeconds);
            while (connected && matchManager != null)
            {
                var httpUrl = ToHttpUrl(serverUrl);
                using var request = UnityWebRequest.Get($"{httpUrl}/match/state?lobbyId={UnityWebRequest.EscapeURL(lobbyId)}&playerId={UnityWebRequest.EscapeURL(playerId)}");
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    ApplyResponseSnapshot(request.downloadHandler?.text ?? string.Empty);
                }

                yield return wait;
            }
        }

        private IEnumerator SendActionRoutine(string action, Dictionary<string, int> payload)
        {
            var payloadJson = MatchSnapshotJson.BuildActionPayload(payload);
            var body =
                "{" +
                $"\"lobbyId\":\"{ApiJson.Escape(lobbyId)}\"," +
                $"\"playerId\":\"{ApiJson.Escape(playerId)}\"," +
                $"\"action\":\"{ApiJson.Escape(action)}\"," +
                $"\"payload\":{payloadJson}" +
                "}";

            using var request = CreatePost($"{ToHttpUrl(serverUrl)}/match/action", body);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                ApplyResponseSnapshot(request.downloadHandler?.text ?? string.Empty);
            }
        }

        private IEnumerator SendCompleteCombatRoutine()
        {
            var body =
                "{" +
                $"\"lobbyId\":\"{ApiJson.Escape(lobbyId)}\"," +
                $"\"playerId\":\"{ApiJson.Escape(playerId)}\"" +
                "}";

            using var request = CreatePost($"{ToHttpUrl(serverUrl)}/match/complete-combat", body);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                ApplyResponseSnapshot(request.downloadHandler?.text ?? string.Empty);
            }
        }

        private void ApplyResponseSnapshot(string response)
        {
            var snapshotJson = ExtractSnapshotJson(response);
            if (string.IsNullOrEmpty(snapshotJson)
                || !MatchSnapshotJson.TryParse(snapshotJson, out var snapshot)
                || snapshot.players == null
                || snapshot.players.Length == 0)
            {
                return;
            }

            if (snapshot.version == lastSnapshotVersion)
            {
                return;
            }

            var wasAwaitingCombat = matchManager.IsAwaitingCombatPlayback;
            matchManager.ApplySnapshot(snapshot, localSlotIndex);
            lastSnapshotVersion = snapshot.version;

            if (!wasAwaitingCombat && snapshot.awaitingCombat && snapshot.pendingCombat != null)
            {
                matchManager.BeginCombatPlaybackFromSnapshot(snapshot.pendingCombat, localSlotIndex);
                CombatPlaybackRequested?.Invoke();
            }

            if (snapshot.matchEnded && snapshot.matchEnd != null)
            {
                connected = false;
            }
        }

        private static UnityWebRequest CreatePost(string url, string body)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        private static string ToHttpUrl(string url) =>
            url.Replace("ws://", "http://", StringComparison.OrdinalIgnoreCase)
                .Replace("wss://", "https://", StringComparison.OrdinalIgnoreCase);

        private static string ExtractSnapshotJson(string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                return string.Empty;
            }

            var marker = "\"snapshot\":";
            var markerIndex = response.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex >= 0)
            {
                return ExtractJsonObject(response, markerIndex);
            }

            if (response.TrimStart().StartsWith("{", StringComparison.Ordinal)
                && response.Contains("\"players\"", StringComparison.Ordinal)
                && response.Contains("\"turn\"", StringComparison.Ordinal))
            {
                return response;
            }

            return string.Empty;
        }

        private static string ExtractJsonObject(string response, int markerIndex)
        {
            var index = response.IndexOf('{', markerIndex);
            if (index < 0)
            {
                return string.Empty;
            }

            var depth = 0;
            for (var i = index; i < response.Length; i++)
            {
                if (response[i] == '{')
                {
                    depth++;
                }
                else if (response[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return response.Substring(index, i - index + 1);
                    }
                }
            }

            return string.Empty;
        }

        private static string BuildSlotsJson(MatchSlot[] slots)
        {
            if (slots == null || slots.Length == 0)
            {
                return "[]";
            }

            var parts = new List<string>();
            foreach (var slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                parts.Add(
                    "{" +
                    $"\"slotIndex\":{slot.slotIndex}," +
                    $"\"isBot\":{(slot.isBot ? "true" : "false")}," +
                    $"\"playerId\":{(string.IsNullOrEmpty(slot.playerId) ? "null" : $"\"{ApiJson.Escape(slot.playerId)}\"")}," +
                    $"\"displayName\":\"{ApiJson.Escape(slot.displayName ?? "Player")}\"" +
                    "}");
            }

            return "[" + string.Join(",", parts) + "]";
        }
    }
}