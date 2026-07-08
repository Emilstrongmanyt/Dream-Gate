using System;
using System.Collections;
using System.Text;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Services;
using DreamGate.Battlegrounds.Services.Backend;
using UnityEngine;
using UnityEngine.Networking;

namespace DreamGate.Battlegrounds.Networking
{
    /// <summary>
    /// Connects rated clients to the match coordination server. Falls back to local simulation
    /// until the authoritative match server session is available.
    /// </summary>
    public class RemoteMatchClient : INetworkMatchHost
    {
        private readonly string serverUrl;
        private readonly string lobbyId;
        private readonly LocalMatchHost fallback = new();

        private MatchManager matchManager;
        private bool connected;

        public bool IsServer => false;
        public MatchMode Mode => MatchMode.Rated;

        public RemoteMatchClient(string serverUrl, string lobbyId)
        {
            this.serverUrl = serverUrl?.TrimEnd('/');
            this.lobbyId = lobbyId;
        }

        public void InitializeMatch(MatchManager manager)
        {
            matchManager = manager;
            fallback.InitializeMatch(manager);
            CloudCoroutineHost.Instance.Run(ConnectRoutine());
        }

        public void TickRecruitTimer(float deltaTime)
        {
            if (!connected)
            {
                fallback.TickRecruitTimer(deltaTime);
                return;
            }

            fallback.TickRecruitTimer(deltaTime);
        }

        public void Dispose()
        {
            fallback.Dispose();
            matchManager = null;
            connected = false;
        }

        private IEnumerator ConnectRoutine()
        {
            if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(lobbyId))
            {
                Debug.LogWarning("RemoteMatchClient missing server URL or lobby id. Using local rated simulation.");
                yield break;
            }

            var body = ApiJson.BuildObject(new System.Collections.Generic.Dictionary<string, object>
            {
                { "lobbyId", lobbyId },
                { "playerId", DreamGateServices.Profile?.playerId ?? string.Empty },
                { "displayName", DreamGateServices.Profile?.displayName ?? "Dreamer" }
            });

            var httpUrl = serverUrl
                .Replace("ws://", "http://", StringComparison.OrdinalIgnoreCase)
                .Replace("wss://", "https://", StringComparison.OrdinalIgnoreCase);
            using var request = new UnityWebRequest($"{httpUrl}/match/join", UnityWebRequest.kHttpVerbPOST);
            var bytes = Encoding.UTF8.GetBytes(body);
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"RemoteMatchClient could not join match server: {request.error}. Using local rated simulation.");
                yield break;
            }

            connected = true;
            Debug.Log($"RemoteMatchClient joined lobby {lobbyId} on {serverUrl}.");
        }
    }
}