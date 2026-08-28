using System;
using System.Collections;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Kindling.Sim.Match;
using Kindling.Sim.Model;

namespace Kindling.Client
{
    public sealed class NetMatchClient : MonoBehaviour
    {
        public string Host;
        public string AuthToken;
        public string LastSnapshot;
        public bool Connected;
        public string MatchId;
        public string ResumeToken;
        public int Seat;
        public string LastHttpError;
        public Action<string> OnSnapshot;
        public Action<string> OnError;
        public Action<string> OnWelcome;
        ClientWebSocket _ws;
        readonly SynchronizationContext _main = SynchronizationContext.Current;
        int _seq = 1;
        int _gen;

        public static string ResolveHost()
        {
            string h = PlayerPrefs.GetString("kindling.host", "");
            if (string.IsNullOrEmpty(h))
                h = Environment.GetEnvironmentVariable("KINDLING_HOST") ?? "";
            return h;
        }

        public IEnumerator PostJson(string path, string json, Action<int, string> done)
        {
            LastHttpError = "";
            string url = (Host ?? "").TrimEnd('/') + path;
            using (var req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(json ?? "{}");
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(AuthToken))
                    req.SetRequestHeader("Authorization", "Bearer " + AuthToken);
                yield return req.SendWebRequest();
                int code = (int)req.responseCode;
                string body = req.downloadHandler != null ? req.downloadHandler.text : "";
                if (req.result != UnityWebRequest.Result.Success && code == 0)
                    LastHttpError = req.error ?? "network";
                done?.Invoke(code, body);
            }
        }

        public IEnumerator Connect(string host)
        {
            Host = (host ?? "").TrimEnd('/');
            yield return QueueMatch();
        }

        public IEnumerator QueueMatch()
        {
            LastHttpError = "";
            if (string.IsNullOrEmpty(Host) || string.IsNullOrEmpty(AuthToken))
            {
                LastHttpError = "unauthorized";
                yield break;
            }
            string qUrl = Host + "/v1/queue";
            string wsUrl = "";
            string matchToken = "";
            using (var req = new UnityWebRequest(qUrl, "POST"))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Bearer " + AuthToken);
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    LastHttpError = string.IsNullOrEmpty(req.downloadHandler.text) ? req.error : req.downloadHandler.text;
                    Debug.LogWarning("queue failed: " + LastHttpError);
                    yield break;
                }
                string body = req.downloadHandler.text;
                wsUrl = Protocol.ReadString(body, "ws");
                matchToken = Protocol.ReadString(body, "token");
                MatchId = Protocol.ReadString(body, "matchId");
                Seat = Protocol.ReadInt(body, "seat");
            }
            if (string.IsNullOrEmpty(wsUrl)) yield break;
            ResumeToken = matchToken;
            yield return OpenSocket(wsUrl);
        }

        public void Disconnect()
        {
            _gen++;
            Connected = false;
            try { _ws?.Abort(); } catch { /* ignore */ }
            _ws = null;
        }

        public IEnumerator Reconnect()
        {
            if (string.IsNullOrEmpty(Host) || string.IsNullOrEmpty(ResumeToken)) yield break;
            Connected = false;
            try { _ws?.Abort(); } catch { /* ignore */ }
            string wsUrl = Host.Replace("http://", "ws://").Replace("https://", "wss://")
                + "/v1/match?id=" + Uri.EscapeDataString(MatchId ?? "")
                + "&seat=" + Seat
                + "&token=" + Uri.EscapeDataString(ResumeToken);
            yield return OpenSocket(wsUrl);
            if (Connected && _ws != null && _ws.State == WebSocketState.Open)
            {
                byte[] data = Encoding.UTF8.GetBytes("{\"op\":\"Reconnect\",\"seq\":" + _seq + "}");
                _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        IEnumerator OpenSocket(string wsUrl)
        {
            _gen++;
            int gen = _gen;
            _ws = new ClientWebSocket();
            var uri = new Uri(wsUrl);
            var connect = _ws.ConnectAsync(uri, CancellationToken.None);
            while (!connect.IsCompleted) yield return null;
            if (gen != _gen) yield break;
            if (_ws.State != WebSocketState.Open)
            {
                Debug.LogWarning("ws connect failed");
                yield break;
            }
            Connected = true;
            StartCoroutine(RecvLoop(gen));
        }

        public void SendAction(RecruitAction a)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            SendRaw(ToJson(a, _seq++));
        }

        public void SendRaw(string json)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            byte[] data = Encoding.UTF8.GetBytes(json ?? "");
            _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        IEnumerator RecvLoop(int gen)
        {
            var buf = new byte[32768];
            while (gen == _gen && _ws != null && _ws.State == WebSocketState.Open)
            {
                var task = _ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                while (!task.IsCompleted) yield return null;
                if (gen != _gen) yield break;
                WebSocketReceiveResult r = task.Result;
                if (r.MessageType == WebSocketMessageType.Close) break;
                string msg = Encoding.UTF8.GetString(buf, 0, r.Count);
                Dispatch(msg);
            }
            if (gen == _gen) Connected = false;
        }

        void Dispatch(string msg)
        {
            string op = Protocol.ReadString(msg, "op");
            if (op == "Welcome")
            {
                string tok = Protocol.ReadString(msg, "deviceResumeToken");
                if (!string.IsNullOrEmpty(tok)) ResumeToken = tok;
                Seat = Protocol.ReadInt(msg, "seat");
                OnWelcome?.Invoke(msg);
                return;
            }
            if (op == "Error")
            {
                OnError?.Invoke(Protocol.ReadString(msg, "code"));
                return;
            }
            LastSnapshot = msg;
            OnSnapshot?.Invoke(msg);
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
