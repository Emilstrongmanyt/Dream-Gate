using System;
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
        static string _prefix = "http://127.0.0.1:5080/";

        public static int Main(string[] args)
        {
            string content = FindContent();
            if (content == null)
            {
                Console.Error.WriteLine("content/ not found");
                return 2;
            }
            _cat = Catalog.LoadFromDirectory(content);
            _queue = new CasualQueue();
            if (args != null && args.Length > 0 && args[0].StartsWith("http"))
                _prefix = args[0];
            if (!_prefix.EndsWith("/")) _prefix += "/";

            var listener = new HttpListener();
            listener.Prefixes.Add(_prefix);
            listener.Start();
            Console.WriteLine("Kindling MatchHost  " + _prefix);
            Console.WriteLine("GET /healthz   POST /v1/queue   WS /v1/match?id=&seat=&token=");

            var tick = new Thread(TickLoop) { IsBackground = true };
            tick.Start();

            while (true)
            {
                HttpListenerContext ctx = listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
            }
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
                    Write(ctx, 200, "text/plain", "ok live=" + _queue.LiveCount);
                    return;
                }
                if (ctx.Request.HttpMethod == "POST" && path == "/v1/queue")
                {
                    string name = ctx.Request.QueryString["name"] ?? "You";
                    MatchSession s = _queue.Enqueue(_cat, name, (uint)Environment.TickCount);
                    string id = s.Loop.State.MatchId.ToString("D");
                    string body = "{\"matchId\":\"" + id + "\",\"seat\":0,\"token\":\"" + s.ResumeTokens[0]
                        + "\",\"ws\":\"" + _prefix.Replace("http://", "ws://").Replace("https://", "wss://")
                        + "v1/match?id=" + id + "&seat=0&token=" + s.ResumeTokens[0] + "\"}";
                    Write(ctx, 200, "application/json", body);
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
                Write(ctx, 404, "text/plain", "not found");
            }
            catch (Exception ex)
            {
                try { Write(ctx, 500, "text/plain", ex.Message); }
                catch { /* ignore */ }
            }
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
            var buf = new byte[8192];
            while (ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                if (r.MessageType == WebSocketMessageType.Close) break;
                string msg = Encoding.UTF8.GetString(buf, 0, r.Count);
                string reply = s.Handle(seat, msg);
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

        static void Write(HttpListenerContext ctx, int status, string type, string body)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = type;
            byte[] data = Encoding.UTF8.GetBytes(body ?? "");
            ctx.Response.ContentLength64 = data.Length;
            ctx.Response.OutputStream.Write(data, 0, data.Length);
            ctx.Response.Close();
        }

        static string FindContent()
        {
            string found = Catalog.FindContentRoot(AppContext.BaseDirectory);
            if (found != null) return found;
            return Catalog.FindContentRoot(Directory.GetCurrentDirectory());
        }
    }
}
