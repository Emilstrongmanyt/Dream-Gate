using System;
using System.Collections;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Kindling.Sim.Match;
using Kindling.Sim.Model;

namespace Kindling.Client
{
    public sealed class NetMatchClient : MonoBehaviour
    {
        public string Host;
        public string LastSnapshot;
        public bool Connected;
        public Action<string> OnSnapshot;
        ClientWebSocket _ws;
        readonly SynchronizationContext _main = SynchronizationContext.Current;
        int _seq = 1;

        public static string ResolveHost()
        {
            string h = PlayerPrefs.GetString("kindling.host", "");
            if (string.IsNullOrEmpty(h))
                h = Environment.GetEnvironmentVariable("KINDLING_HOST") ?? "";
            return h;
        }

        public IEnumerator Connect(string host)
        {
            Host = host.TrimEnd('/');
            string device = SystemInfo.deviceUniqueIdentifier;
            string authUrl = Host + "/v1/auth/device";
            string token = "";
            using (var req = UnityEngine.Networking.UnityWebRequest.PostWwwForm(authUrl + "?deviceId=" + Uri.EscapeDataString(device) + "&name=You", ""))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    token = Protocol.ReadString(req.downloadHandler.text, "token");
            }
            string qUrl = Host + "/v1/queue";
            string wsUrl = "";
            string matchToken = "";
            using (var req = new UnityEngine.Networking.UnityWebRequest(qUrl, "POST"))
            {
                req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                if (!string.IsNullOrEmpty(token))
                    req.SetRequestHeader("Authorization", "Bearer " + token);
                yield return req.SendWebRequest();
                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("queue failed: " + req.error);
                    yield break;
                }
                wsUrl = Protocol.ReadString(req.downloadHandler.text, "ws");
                matchToken = Protocol.ReadString(req.downloadHandler.text, "token");
            }
            if (string.IsNullOrEmpty(wsUrl)) yield break;
            _ws = new ClientWebSocket();
            var uri = new Uri(wsUrl);
            var connect = _ws.ConnectAsync(uri, CancellationToken.None);
            while (!connect.IsCompleted) yield return null;
            if (_ws.State != WebSocketState.Open)
            {
                Debug.LogWarning("ws connect failed");
                yield break;
            }
            Connected = true;
            StartCoroutine(RecvLoop());
        }

        public void SendAction(RecruitAction a)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            string json = ToJson(a, _seq++);
            byte[] data = Encoding.UTF8.GetBytes(json);
            _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        IEnumerator RecvLoop()
        {
            var buf = new byte[16384];
            while (_ws != null && _ws.State == WebSocketState.Open)
            {
                var task = _ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                while (!task.IsCompleted) yield return null;
                WebSocketReceiveResult r = task.Result;
                if (r.MessageType == WebSocketMessageType.Close) break;
                string msg = Encoding.UTF8.GetString(buf, 0, r.Count);
                LastSnapshot = msg;
                OnSnapshot?.Invoke(msg);
            }
            Connected = false;
        }

        static string ToJson(RecruitAction a, int seq)
        {
            string op = a.Op.ToString();
            var sb = new StringBuilder();
            sb.Append("{\"op\":\"").Append(op).Append("\",\"seq\":").Append(seq);
            sb.Append(",\"stallIndex\":").Append(a.StallIndex);
            sb.Append(",\"destIndex\":").Append(a.DestIndex);
            sb.Append(",\"handIndex\":").Append(a.HandIndex);
            sb.Append(",\"offerIndex\":").Append(a.OfferIndex);
            sb.Append(",\"hostIndex\":").Append(a.HostIndex);
            sb.Append(",\"fromIndex\":").Append(a.FromIndex);
            sb.Append(",\"index\":").Append(a.Index);
            sb.Append(",\"targetIndex\":").Append(a.TargetIndex);
            sb.Append(",\"held\":").Append(a.Held ? "true" : "false");
            if (a.Dest != 0) sb.Append(",\"dest\":\"").Append(a.Dest).Append('"');
            if (a.Loc != 0) sb.Append(",\"loc\":\"").Append(a.Loc).Append('"');
            if (a.From != 0) sb.Append(",\"from\":\"").Append(a.From).Append('"');
            if (!string.IsNullOrEmpty(a.CaptainId)) sb.Append(",\"captainId\":\"").Append(a.CaptainId).Append('"');
            sb.Append('}');
            return sb.ToString();
        }
    }
}
