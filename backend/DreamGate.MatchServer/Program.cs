using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using DreamGate.Battlegrounds.Networking;
using DreamGate.Battlegrounds.Services;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var sessions = new ConcurrentDictionary<string, RatedMatchSession>();
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

app.MapGet("/health", () => Results.Json(new { ok = true, matches = sessions.Count }));

app.MapPost("/match/join", (JoinMatchRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.LobbyId) || string.IsNullOrWhiteSpace(request.PlayerId))
    {
        return Results.BadRequest(new { error = "lobbyId and playerId are required." });
    }

    var session = sessions.GetOrAdd(request.LobbyId, _ =>
        new RatedMatchSession(request.LobbyId, request.MatchSeed, request.Slots ?? Array.Empty<MatchSlot>()));

    return Results.Json(new
    {
        authoritative = true,
        lobbyId = request.LobbyId,
        matchSeed = request.MatchSeed,
        snapshot = session.GetSnapshot(request.PlayerId)
    }, jsonOptions);
});

app.MapGet("/match/state", (string lobbyId, string playerId) =>
{
    if (!sessions.TryGetValue(lobbyId, out var session))
    {
        return Results.NotFound(new { error = "Lobby not found." });
    }

    return Results.Json(session.GetSnapshot(playerId), jsonOptions);
});

app.MapPost("/match/action", (MatchActionRequest request) =>
{
    if (!sessions.TryGetValue(request.LobbyId, out var session))
    {
        return Results.NotFound(new { error = "Lobby not found." });
    }

    var payload = request.Payload?.ToDictionary(
        pair => pair.Key,
        pair => pair.Value.ToString() ?? "0") ?? new Dictionary<string, string>();

    var success = session.TryApplyAction(request.PlayerId, request.Action, payload, out var message);
    return Results.Json(new
    {
        success,
        message,
        snapshot = session.GetSnapshot(request.PlayerId)
    }, jsonOptions);
});

app.MapPost("/match/complete-combat", (CompleteCombatRequest request) =>
{
    if (!sessions.TryGetValue(request.LobbyId, out var session))
    {
        return Results.NotFound(new { error = "Lobby not found." });
    }

    var success = session.TryCompleteCombat(request.PlayerId, out var message);
    return Results.Json(new
    {
        success,
        message,
        snapshot = session.GetSnapshot(request.PlayerId)
    }, jsonOptions);
});

_ = Task.Run(async () =>
{
    while (true)
    {
        foreach (var session in sessions.Values)
        {
            session.Tick(0.1f);
        }

        await Task.Delay(100);
    }
});

var port = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var parsedPort) ? parsedPort : 8787;
app.Urls.Add($"http://0.0.0.0:{port}");
Console.WriteLine($"Dream Gate authoritative match server on :{port}");
app.Run();

record JoinMatchRequest(string LobbyId, string PlayerId, string DisplayName, int MatchSeed, MatchSlot[] Slots);
record MatchActionRequest(string LobbyId, string PlayerId, string Action, Dictionary<string, int>? Payload);
record CompleteCombatRequest(string LobbyId, string PlayerId);