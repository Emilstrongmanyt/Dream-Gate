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
        static readonly List<ClientConn> _clients = new List<ClientConn>();
        static readonly object _clientsGate = new object();

        sealed class ClientConn
        {
            public WebSocket Ws;
            public string MatchId;
            public int Seat;
            public readonly object SendGate = new object();
        }

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
            Console.WriteLine("GET /healthz  POST /v1/auth/register  POST /v1/auth/login  POST /v1/queue  WS /v1/match");

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
            string dir = Environment.GetEnvironmentVariable("KINDLING_STORE") ?? Path.Combine(AppContext.BaseDirectory, "store");
            IMatchStore files = new FileMatchStore(dir);
            IMatchStore matches = files;
            IMatchStore accounts = files;
            string redis = Environment.GetEnvironmentVariable("REDIS_URL");
            if (!string.IsNullOrEmpty(redis))
            {
                try
                {
                    matches = new RedisMatchStore(redis);
                    accounts = matches;
                    Console.WriteLine("store=redis");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("redis failed, using files: " + ex.Message);
                }
            }
            string pg = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (!string.IsNullOrEmpty(pg))
            {
                try
                {
                    accounts = new PostgresMatchStore(pg);
                    if (matches == files) matches = accounts;
                    Console.WriteLine("store=postgres accounts");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("postgres failed: " + ex.Message);
                }
            }
            if (matches == accounts) return matches;
            return new CompositeMatchStore(matches, accounts);
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
                try
                {
                    _queue.TickAll(DateTime.UtcNow, PushSnapshot);
                }
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
                if (ctx.Request.HttpMethod == "POST" && path == "/v1/auth/register")
                {
                    AuthRegister(ctx);
                    return;
                }
                if (ctx.Request.HttpMethod == "POST" && path == "/v1/auth/login")
                {
                    AuthLogin(ctx);
                    return;
                }
                if (ctx.Request.HttpMethod == "GET" && path == "/v1/config")
                {
                    Write(ctx, 200, "application/json", LiveConfig.Json(_cat));
                    return;
                }
                if (ctx.Request.HttpMethod == "POST" && path == "/v1/queue")
                {
                    Queue(ctx);
                    return;
                }
                if (ctx.Request.HttpMethod == "DELETE" && path == "/v1/queue")
                {
                    Write(ctx, 200, "application/json", "{\"cancelled\":false,\"reason\":\"already_started\"}");
                    return;
                }
                if (ctx.Request.HttpMethod == "GET" && path == "/v1/history")
                {
                    string tokH = Bearer(ctx);
                    if (!DeviceAuth.Verify(tokH, _pepper)) { Write(ctx, 401, "text/plain", "unauthorized"); return; }
                    string accH = DeviceAuth.AccountId(tokH);
                    Write(ctx, 200, "application/json", _store.ListHistory(accH) ?? "[]");
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
                if (ctx.Request.HttpMethod == "POST" && path == "/v1/cosmetics/equip")
                {
                    CosmeticsEquip(ctx);
                    return;
                }
                if (ctx.Request.HttpMethod == "POST" && path == "/v1/cosmetics/debug")
                {
                    CosmeticsDebug(ctx);
                    return;
                }
                if (ctx.Request.HttpMethod == "POST" && path == "/v1/iap/receipt")
                {
                    IapReceipt(ctx);
                    return;
                }
                if (ctx.Request.HttpMethod == "GET" && path == "/v1/me")
                {
                    string tok = Bearer(ctx);
                    if (!DeviceAuth.Verify(tok, _pepper)) { Write(ctx, 401, "text/plain", "unauthorized"); return; }
                    string acc = DeviceAuth.AccountId(tok);
                    string json = _store.GetAccount(acc) ?? "{}";
                    Write(ctx, 200, "application/json", AccountAuth.PublicJson(json));
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

        static string RequireAccount(HttpListenerContext ctx)
        {
            string tok = Bearer(ctx);
            if (string.IsNullOrEmpty(tok) || !DeviceAuth.Verify(tok, _pepper))
            {
                Write(ctx, 401, "text/plain", "unauthorized");
                return null;
            }
            return DeviceAuth.AccountId(tok);
        }

        static void CosmeticsEquip(HttpListenerContext ctx)
        {
            string acc = RequireAccount(ctx);
            if (acc == null) return;
            string body = ReadBody(ctx);
            string frame = Protocol.ReadString(body, "frame");
            string prev = _store.GetAccount(acc) ?? "{}";
            string next = Cosmetics.PatchEquip(prev, frame);
            _store.PutAccount(acc, next);
            Write(ctx, 200, "application/json", AccountAuth.PublicJson(next));
        }

        static void CosmeticsDebug(HttpListenerContext ctx)
        {
            string acc = RequireAccount(ctx);
            if (acc == null) return;
            string prev = _store.GetAccount(acc) ?? "{}";
            string next = Cosmetics.PatchEquip(prev, "gold");
            next = AccountAuth.WithCosmetic(next, Cosmetics.GrantAll(), Protocol.ReadString(next, "frame"));
            _store.PutAccount(acc, next);
            Write(ctx, 200, "application/json", AccountAuth.PublicJson(next));
        }

        static void IapReceipt(HttpListenerContext ctx)
        {
            string acc = RequireAccount(ctx);
            if (acc == null) return;
            string body = ReadBody(ctx);
            string product = Protocol.ReadString(body, "productId");
            if (string.IsNullOrEmpty(product)) product = Protocol.ReadString(body, "sku");
            string frame = "gold";
            if (product.IndexOf("ember", StringComparison.OrdinalIgnoreCase) >= 0) frame = "ember";
            else if (product.IndexOf("spirit", StringComparison.OrdinalIgnoreCase) >= 0) frame = "spirit";
            else if (product.IndexOf("wick", StringComparison.OrdinalIgnoreCase) >= 0) frame = "wick";
            else if (product.IndexOf("night", StringComparison.OrdinalIgnoreCase) >= 0) frame = "night";
            string prev = _store.GetAccount(acc) ?? "{}";
            string next = Cosmetics.PatchEquip(prev, frame);
            _store.PutAccount(acc, next);
            Write(ctx, 200, "application/json", "{\"ok\":true,\"sandbox\":true,\"frame\":\"" + frame
                + "\",\"account\":" + AccountAuth.PublicJson(next) + "}");
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
            Write(ctx, 200, "application/json", "{\"token\":\"" + token + "\",\"account\":" + AccountAuth.PublicJson(profile) + "}");
        }

        static void AuthRegister(HttpListenerContext ctx)
        {
            string body = ReadBody(ctx);
            string name = Protocol.ReadString(body, "displayName");
            if (string.IsNullOrEmpty(name)) name = Protocol.ReadString(body, "name");
            string pass = Protocol.ReadString(body, "password");
            string badName = AccountAuth.ValidateName(name);
            if (badName != null) { Write(ctx, 400, "application/json", "{\"error\":\"" + badName + "\"}"); return; }
            string badPass = AccountAuth.ValidatePassword(pass);
            if (badPass != null) { Write(ctx, 400, "application/json", "{\"error\":\"" + badPass + "\"}"); return; }
            string login = AccountAuth.NormalizeLogin(name);
            if (!string.IsNullOrEmpty(_store.GetLogin(login)))
            {
                Write(ctx, 409, "application/json", "{\"error\":\"NAME_TAKEN\"}");
                return;
            }
            string acc = DeviceAuth.NewAccountId();
            string salt = AccountAuth.NewSalt();
            string hash = AccountAuth.HashPassword(pass, _pepper, salt);
            string deviceId = Protocol.ReadString(body, "deviceId");
            string deviceHash = string.IsNullOrEmpty(deviceId) ? "" : DeviceAuth.HashDevice(deviceId, _pepper);
            string json = AccountAuth.CreateAccount(acc, name.Trim(), login, salt, hash, deviceHash);
            _store.PutAccount(acc, json);
            _store.PutLogin(login, acc);
            if (!string.IsNullOrEmpty(deviceHash))
                _store.PutDevice(deviceHash, acc);
            string token = DeviceAuth.IssueToken(acc, _pepper);
            Write(ctx, 200, "application/json", "{\"token\":\"" + token + "\",\"account\":" + AccountAuth.PublicJson(json) + "}");
        }

        static void AuthLogin(HttpListenerContext ctx)
        {
            string body = ReadBody(ctx);
            string name = Protocol.ReadString(body, "displayName");
            if (string.IsNullOrEmpty(name)) name = Protocol.ReadString(body, "name");
            string pass = Protocol.ReadString(body, "password");
            string login = AccountAuth.NormalizeLogin(name);
            string acc = _store.GetLogin(login);
            if (string.IsNullOrEmpty(acc))
            {
                Write(ctx, 401, "application/json", "{\"error\":\"BAD_LOGIN\"}");
                return;
            }
            string json = _store.GetAccount(acc) ?? "{}";
            string salt = Protocol.ReadString(json, "passSalt");
            string hash = Protocol.ReadString(json, "passHash");
            if (!AccountAuth.VerifyPassword(pass, _pepper, salt, hash))
            {
                Write(ctx, 401, "application/json", "{\"error\":\"BAD_LOGIN\"}");
                return;
            }
            string token = DeviceAuth.IssueToken(acc, _pepper);
            Write(ctx, 200, "application/json", "{\"token\":\"" + token + "\",\"account\":" + AccountAuth.PublicJson(json) + "}");
        }

        static void Queue(HttpListenerContext ctx)
        {
            string tok = Bearer(ctx);
            if (string.IsNullOrEmpty(tok) || !DeviceAuth.Verify(tok, _pepper))
            {
                Write(ctx, 401, "text/plain", "unauthorized");
                return;
            }
            string name = ctx.Request.QueryString["name"] ?? "You";
            {
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
            string accId = !string.IsNullOrEmpty(tok) ? DeviceAuth.AccountId(tok) : null;
            MatchSession s = _queue.Enqueue(_cat, name, (uint)Environment.TickCount, accId);
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
            if (s == null && !string.IsNullOrEmpty(token))
                s = _queue.GetByToken(token);
            if (s == null || seat < 0 || seat >= 8 || s.ResumeTokens[seat] != token)
            {
                await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "bad token", CancellationToken.None);
                return;
            }
            string matchId = s.Loop.State.MatchId.ToString("D");
            var conn = new ClientConn { Ws = ws, MatchId = matchId, Seat = seat };
            lock (_clientsGate) _clients.Add(conn);
            try
            {
                SendLocked(conn, s.Handle(seat, "{\"op\":\"Join\"}"));
                SendLocked(conn, s.SnapshotFor(seat, s.LastSeq[seat]));
                var buf = new byte[16384];
                while (ws.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    string msg = Encoding.UTF8.GetString(buf, 0, r.Count);
                    string reply = s.Handle(seat, msg);
                    _queue.Persist(s);
                    SendLocked(conn, reply);
                    if (s.Loop.State.MatchOver)
                        PushSnapshot(s);
                }
            }
            finally
            {
                lock (_clientsGate) _clients.Remove(conn);
            }
            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        }

        static void PushSnapshot(MatchSession s)
        {
            if (s == null) return;
            string id = s.Loop.State.MatchId.ToString("D");
            ClientConn[] snap;
            lock (_clientsGate)
                snap = _clients.ToArray();
            for (int i = 0; i < snap.Length; i++)
            {
                ClientConn c = snap[i];
                if (c.MatchId != id) continue;
                try { SendLocked(c, s.SnapshotFor(c.Seat, s.LastSeq[c.Seat])); }
                catch (Exception ex) { Console.Error.WriteLine(ex.Message); }
            }
        }

        static void SendLocked(ClientConn c, string text)
        {
            if (c == null || c.Ws == null) return;
            lock (c.SendGate)
            {
                if (c.Ws.State != WebSocketState.Open) return;
                byte[] data = Encoding.UTF8.GetBytes(text ?? "");
                c.Ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
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
