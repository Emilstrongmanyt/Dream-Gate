import { createClient } from "https://esm.sh/@supabase/supabase-js@2.49.1";

const TARGET_PLAYERS = 8;
const QUEUE_TIMEOUT_SECONDS = 30;
const INITIAL_MMR_RANGE = 200;

type MatchSlot = {
  slotIndex: number;
  isBot: boolean;
  playerId: string | null;
  displayName: string;
};

type QueueEntry = {
  player_id: string;
  mmr: number;
  display_name: string;
  queued_at: string;
};

Deno.serve(async (req) => {
  if (req.method !== "POST") {
    return json({ error: "Method not allowed" }, 405);
  }

  const supabaseUrl = Deno.env.get("SUPABASE_URL");
  const supabaseAnonKey = Deno.env.get("SUPABASE_ANON_KEY");
  const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  const matchServerUrl = Deno.env.get("MATCH_SERVER_URL") ?? "";

  if (!supabaseUrl || !supabaseAnonKey || !serviceRoleKey) {
    return json({ error: "Server misconfigured" }, 500);
  }

  const authHeader = req.headers.get("Authorization") ?? "";
  const userClient = createClient(supabaseUrl, supabaseAnonKey, {
    global: { headers: { Authorization: authHeader } },
  });
  const admin = createClient(supabaseUrl, serviceRoleKey);

  const { data: userData, error: userError } = await userClient.auth.getUser();
  if (userError || !userData.user) {
    return json({ error: "Unauthorized" }, 401);
  }

  const body = await req.json();
  const action = body.action as string;

  if (action === "cancel") {
    await admin.from("match_queue").delete().eq("player_id", userData.user.id);
    return json({ status: "cancelled" });
  }

  if (action === "join") {
    const displayName = (body.displayName as string) || "Dreamer";
    const mmr = Number(body.mmr ?? 1500);

    await admin.from("match_queue").delete().eq("player_id", userData.user.id);
    const { error: insertError } = await admin.from("match_queue").insert({
      player_id: userData.user.id,
      mmr,
      display_name: displayName,
      status: "searching",
    });
    if (insertError) {
      return json({ error: insertError.message }, 400);
    }
  }

  if (action !== "join" && action !== "poll") {
    return json({ error: "Unknown action" }, 400);
  }

  const { data: myQueue, error: queueError } = await admin
    .from("match_queue")
    .select("player_id, mmr, display_name, queued_at, status")
    .eq("player_id", userData.user.id)
    .maybeSingle();

  if (queueError) {
    return json({ error: queueError.message }, 400);
  }

  if (!myQueue || myQueue.status !== "searching") {
    return json({ status: "idle", humansFound: 0, targetPlayers: TARGET_PLAYERS });
  }

  const queuedAt = new Date(myQueue.queued_at).getTime();
  const elapsedSeconds = (Date.now() - queuedAt) / 1000;
  const mmrRange = INITIAL_MMR_RANGE + Math.floor(elapsedSeconds / 5) * 50;

  const { data: candidates, error: candidateError } = await admin
    .from("match_queue")
    .select("player_id, mmr, display_name, queued_at")
    .eq("status", "searching")
    .gte("mmr", myQueue.mmr - mmrRange)
    .lte("mmr", myQueue.mmr + mmrRange)
    .order("queued_at", { ascending: true })
    .limit(TARGET_PLAYERS);

  if (candidateError) {
    return json({ error: candidateError.message }, 400);
  }

  const humans = (candidates ?? []) as QueueEntry[];
  const humansFound = humans.length;
  const oldestQueuedAt = humans.length > 0
    ? Math.min(...humans.map((h) => new Date(h.queued_at).getTime()))
    : queuedAt;
  const oldestElapsedSeconds = (Date.now() - oldestQueuedAt) / 1000;
  const shouldStart = humansFound >= TARGET_PLAYERS || oldestElapsedSeconds >= QUEUE_TIMEOUT_SECONDS;

  if (!shouldStart) {
    return json({
      status: "searching",
      humansFound,
      targetPlayers: TARGET_PLAYERS,
      secondsRemaining: Math.max(0, Math.ceil(QUEUE_TIMEOUT_SECONDS - oldestElapsedSeconds)),
    });
  }

  const lobbyHumans = humans.slice(0, TARGET_PLAYERS);
  const usedBotFill = lobbyHumans.length < TARGET_PLAYERS;
  const lobbyId = crypto.randomUUID().replace(/-/g, "");
  const matchSeed = Math.floor(Math.random() * 2_000_000_000) + 1;

  const { data: matchRow, error: matchError } = await admin
    .from("matches")
    .insert({
      lobby_id: lobbyId,
      match_seed: matchSeed,
      used_bot_fill: usedBotFill,
      human_count: lobbyHumans.length,
      status: "pending",
      match_server_url: matchServerUrl || null,
    })
    .select("id")
    .single();

  if (matchError || !matchRow) {
    return json({ error: matchError?.message ?? "Failed to create match" }, 500);
  }

  const slots: MatchSlot[] = [];
  for (let slotIndex = 0; slotIndex < TARGET_PLAYERS; slotIndex++) {
    const human = lobbyHumans[slotIndex];
    if (human) {
      slots.push({
        slotIndex,
        isBot: false,
        playerId: human.player_id,
        displayName: human.display_name,
      });
    } else {
      slots.push({
        slotIndex,
        isBot: true,
        playerId: null,
        displayName: `Bot ${slotIndex + 1}`,
      });
    }
  }

  const slotRows = slots.map((slot) => ({
    match_id: matchRow.id,
    slot_index: slot.slotIndex,
    is_bot: slot.isBot,
    player_id: slot.playerId,
    display_name: slot.displayName,
  }));

  const { error: slotError } = await admin.from("match_slots").insert(slotRows);
  if (slotError) {
    return json({ error: slotError.message }, 500);
  }

  const humanIds = lobbyHumans.map((h) => h.player_id);
  await admin.from("match_queue").update({ status: "matched" }).in("player_id", humanIds);
  await admin.from("match_queue").delete().in("player_id", humanIds);

  return json({
    status: "matched",
    lobbyId,
    matchSeed,
    humansFound: lobbyHumans.length,
    targetPlayers: TARGET_PLAYERS,
    usedBotFill,
    matchServerUrl: matchServerUrl || null,
    humanSlotIndex: slots.findIndex((s) => s.playerId === userData.user.id),
    slots,
  });
});

function json(payload: unknown, status = 200) {
  return new Response(JSON.stringify(payload), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}