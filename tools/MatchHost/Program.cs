using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kindling.Sim.Catalog;
using Kindling.Sim.Match;

namespace Kindling.Tools.MatchHost
{
    public static class Program
    {
        static Catalog _cat;
        static CasualQueue _queue;
        static IMatchStore _store;
        static string _prefix;
        static string _pepper;
        static readonly Dictionary<string, DateTime> _lastQueue = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        static readonly object _rate = new object();

        public static int Main(string[] args)
        {
            string content = FindContent();
            if (content == null)
            {
                Console.Error.WriteLine("content/ not found");
                return 2;
            }
            _cat = Catalog.LoadFromDirectory(content);
            _pepper = Environment.GetEnvironmentVariable("KINDLING_PEPPER") ?? "kindling-dev-pepper";
            _store = BuildStore();
            _queue = new CasualQueue { Store = _store, CatForRestore = _cat };
            _prefix = PrefixFromEnv(args);
            if (!_prefix.EndsWith("/")) _prefix += "/";

            var listener = new HttpListener();
            listener.Prefixes.Add(_prefix);
            listener.Start();
            Console.WriteLine("Kindling MatchHost  " + _prefix);
            Console.WriteLine("GET /healthz  POST /v1/auth/device  POST /v1/queue  WS /v1/match");

            var tick = new Thread(TickLoop) { IsBackground = true };
            tick.Start();

            while (true)
            {
                HttpListenerContext ctx = listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
            }
        }

        static IMatchStore BuildStore()
        {
            string redis = Environment.GetEnvironmentVariable("REDIS_URL");
            if (!string.IsNullOrEmpty(redis))
            {
                try
                {
                    var r = new RedisMatchStore(redis);
                    Console.WriteLine("store=redis");
                    return r;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("redis failed, using files: " + ex.Message);
                }
            }
            string dir = Environment.GetEnvironmentVariable("KINDLING_STORE") ?? Path.Combine(AppContext.BaseDirectory, "store");
            Console.WriteLine("store=file " + dir);
            return new FileMatchStore(dir);
        }

        static string PrefixFromEnv(string[] args)
        {
            if (args != null && args.Length > 0 && args[0].StartsWith("http"))
                return args[0];
            string port = Environment.GetEnvironmentVariable("PORT");
            if (!string.IsNullOrEmpty(port))
                return "http://+:" + port + "/";
            return "http://127.0.0.1:5080/";
        }

        static void TickLoop()
        {
            while (true)
            {
                try { _queue.TickAll(DateTime.UtcNow); }
                catch (Exception ex) { Console.Error.WriteLine(ex.Message); }
                Thread.Sleep(200);
            }
        }

        static void Handle(HttpListenerContext ctx)
        {
            try
            {
                string path = ctx.Request.Url.AbsolutePath.TrimEnd('/');
                if (path.Length == 0) path = "/";
                if (ctx.Request.IsWebSocketRequest)
                {
                    HandleWs(ctx).GetAwaiter().GetResult();
                    return;
                }
                if (ctx.Request.HttpMethod == "GET" && (path == "/healthz" || path == "/"))
                {
                    Write(ctx, 200, "application/json", "{\"ok\":true,\"live\":" + _queue.LiveCount + ",\"telemetry\":" + Telemetry.Snapshot() + "}");
                    return;
                }
                if (ctx.Request.HttpMethod == "GET" && path == "/v1/metrics")
                {
                    Write(ctx, 200, "application/json", Telemetry.Snapshot());
                    return;
                }
                if (ctx.Request.HttpMethod == "POST" && path == "/v1/auth/device")
                {
                    AuthDevice(ctx);
                    return;
                }
                if (ctx.Request.HttpMethod == "POST" && path == "/v1/queue")
                {
                    Queue(ctx);
                    return;
                }
                if (ctx.Request.HttpMethod == "GET" && path.StartsWith("/v1/match/"))
                {
                    string id = path.Substring("/v1/match/".Length);
                    MatchSession s = _queue.Get(id);
                    if (s == null) { Write(ctx, 404, "text/plain", "not found"); return; }
                    int seat = 0;
                    int.TryParse(ctx.Request.QueryString["seat"], out seat);
                    Write(ctx, 200, "application/json", s.SnapshotFor(seat, s.LastSeq[seat]));
                    return;
                }
                if (ctx.Request.HttpMethod == "GET" && path == "/v1/me")
                {
                    string tok = Bearer(ctx);
                    if (!DeviceAuth.Verify(tok, _pepper)) { Write(ctx, 401, "text/plain", "unauthorized"); return; }
                    string acc = DeviceAuth.AccountId(tok);
                    string json = _store.GetAccount(acc) ?? "{}";
                    Write(ctx, 200, "application/json", json);
                    return;
                }
                Write(ctx, 404, "text/plain", "not found");
            }
            catch (Exception ex)
            {
                try { Write(ctx, 500, "text/plain", ex.Message); }
                catch { /* ignore */ }
            }
        }

        static void AuthDevice(HttpListenerContext ctx)
        {
            string body = ReadBody(ctx);
            string deviceId = Protocol.ReadString(body, "deviceId");
            if (string.IsNullOrEmpty(deviceId))
                deviceId = ctx.Request.QueryString["deviceId"] ?? Guid.NewGuid().ToString("N");
            string name = Protocol.ReadString(body, "displayName");
            if (string.IsNullOrEmpty(name)) name = ctx.Request.QueryString["name"] ?? "Captain";
            string hash = DeviceAuth.HashDevice(deviceId, _pepper);
            string acc = _store.GetDevice(hash);
            if (string.IsNullOrEmpty(acc))
            {
                acc = DeviceAuth.NewAccountId();
                _store.PutDevice(hash, acc);
                _store.PutAccount(acc, "{\"id\":\"" + acc + "\",\"displayName\":\"" + Esc(name)
                    + "\",\"mmr\":1500,\"rd\":350,\"deviceHash\":\"" + hash + "\"}");
            }
            string token = DeviceAuth.IssueToken(acc, _pepper);
            string profile = _store.GetAccount(acc) ?? "{}";
            Write(ctx, 200, "application/json", "{\"token\":\"" + token + "\",\"account\":" + profile + "}");
        }

        static void Queue(HttpListenerContext ctx)
        {
            string tok = Bearer(ctx);
            string name = ctx.Request.QueryString["name"] ?? "You";
            if (!string.IsNullOrEmpty(tok))
            {
                if (!DeviceAuth.Verify(tok, _pepper)) { Write(ctx, 401, "text/plain", "unauthorized"); return; }
                string acc = DeviceAuth.AccountId(tok);
                lock (_rate)
                {
                    DateTime last;
                    if (_lastQueue.TryGetValue(acc, out last) && (DateTime.UtcNow - last).TotalSeconds < 2)
                    {
                        Write(ctx, 429, "text/plain", "rate_limited");
                        return;
                    }
                    _lastQueue[acc] = DateTime.UtcNow;
                }
                string profile = _store.GetAccount(acc);
                if (!string.IsNullOrEmpty(profile))
                {
                    string n = Protocol.ReadString(profile, "displayName");
                    if (!string.IsNullOrEmpty(n)) name = n;
                }
            }
            MatchSession s = _queue.Enqueue(_cat, name, (uint)Environment.TickCount);
            if (!string.IsNullOrEmpty(tok))
            {
                string acc = DeviceAuth.AccountId(tok);
                string profile = _store.GetAccount(acc);
                if (!string.IsNullOrEmpty(profile))
                {
                    s.Loop.Human.Rating = Protocol.ReadInt(profile, "mmr");
                    if (s.Loop.Human.Rating < 100) s.Loop.Human.Rating = 1500;
                    int rd = Protocol.ReadInt(profile, "rd");
                    s.Loop.Human.Rd = rd > 0 ? rd : 350;
                }
            }
            string id = s.Loop.State.MatchId.ToString("D");
            string ws = _prefix.Replace("http://", "ws://").Replace("https://", "wss://")
                + "v1/match?id=" + id + "&seat=0&token=" + s.ResumeTokens[0];
            Write(ctx, 200, "application/json", "{\"matchId\":\"" + id + "\",\"seat\":0,\"token\":\""
                + s.ResumeTokens[0] + "\",\"ws\":\"" + ws + "\"}");
        }

        static async Task HandleWs(HttpListenerContext ctx)
        {
            HttpListenerWebSocketContext wsCtx = await ctx.AcceptWebSocketAsync(null);
            WebSocket ws = wsCtx.WebSocket;
            string id = ctx.Request.QueryString["id"];
            int seat = 0;
            int.TryParse(ctx.Request.QueryString["seat"], out seat);
            string token = ctx.Request.QueryString["token"];
            MatchSession s = _queue.Get(id);
            if (s == null || seat < 0 || seat >= 8 || s.ResumeTokens[seat] != token)
            {
                await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "bad token", CancellationToken.None);
                return;
            }
            string welcome = s.Handle(seat, "{\"op\":\"Join\"}");
            await Send(ws, welcome);
            var buf = new byte[16384];
            while (ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                if (r.MessageType == WebSocketMessageType.Close) break;
                string msg = Encoding.UTF8.GetString(buf, 0, r.Count);
                string reply = s.Handle(seat, msg);
                _queue.Persist(s);
                await Send(ws, reply);
            }
            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        }

        static async Task Send(WebSocket ws, string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text ?? "");
            await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        static string Bearer(HttpListenerContext ctx)
        {
            string h = ctx.Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(h)) return ctx.Request.QueryString["token"] ?? "";
            if (h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return h.Substring(7).Trim();
            return h;
        }

        static string ReadBody(HttpListenerContext ctx)
        {
            using (var sr = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                return sr.ReadToEnd();
        }

        static void Write(HttpListenerContext ctx, int status, string type, string body)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = type;
            byte[] data = Encoding.UTF8.GetBytes(body ?? "");
            ctx.Response.ContentLength64 = data.Length;
            ctx.Response.OutputStream.Write(data, 0, data.Length);
            ctx.Response.Close();
        }

        static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        static string FindContent()
        {
            string found = Catalog.FindContentRoot(AppContext.BaseDirectory);
            if (found != null) return found;
            return Catalog.FindContentRoot(Directory.GetCurrentDirectory());
        }
    }
}
