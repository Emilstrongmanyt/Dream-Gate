import express from "express";
import http from "http";
import { WebSocketServer } from "ws";

const PORT = Number(process.env.PORT ?? 8787);
const app = express();
app.use(express.json());

/** @type {Map<string, { lobbyId: string, seed: number, humans: Map<string, any>, sockets: Set<any> }>} */
const lobbies = new Map();

app.get("/health", (_req, res) => {
  res.json({ ok: true, lobbies: lobbies.size });
});

app.post("/match/join", (req, res) => {
  const { lobbyId, playerId, displayName } = req.body ?? {};
  if (!lobbyId || !playerId) {
    res.status(400).json({ error: "lobbyId and playerId are required." });
    return;
  }

  let lobby = lobbies.get(lobbyId);
  if (!lobby) {
    lobby = {
      lobbyId,
      seed: Number(req.body?.matchSeed ?? Math.floor(Math.random() * 2_000_000_000) + 1),
      humans: new Map(),
      sockets: new Set(),
    };
    lobbies.set(lobbyId, lobby);
  }

  lobby.humans.set(playerId, { playerId, displayName: displayName ?? "Dreamer", joinedAt: Date.now() });
  res.json({
    lobbyId,
    seed: lobby.seed,
    humansConnected: lobby.humans.size,
    status: "waiting_for_authoritative_sim",
  });
});

const server = http.createServer(app);
const wss = new WebSocketServer({ server, path: "/ws" });

wss.on("connection", (socket, request) => {
  const url = new URL(request.url ?? "", `http://${request.headers.host}`);
  const lobbyId = url.searchParams.get("lobbyId");
  const playerId = url.searchParams.get("playerId");
  if (!lobbyId || !playerId) {
    socket.close(1008, "lobbyId and playerId required");
    return;
  }

  const lobby = lobbies.get(lobbyId);
  if (!lobby) {
    socket.close(1008, "Lobby not found");
    return;
  }

  lobby.sockets.add(socket);
  socket.send(JSON.stringify({ type: "welcome", lobbyId, seed: lobby.seed }));

  socket.on("close", () => {
    lobby.sockets.delete(socket);
  });
});

server.listen(PORT, () => {
  console.log(`Dream Gate match server listening on :${PORT}`);
});