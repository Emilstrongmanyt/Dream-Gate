import { createClient } from "https://esm.sh/@supabase/supabase-js@2.49.1";

const PLACEMENT_DELTA = [0, 40, 20, 10, 0, -10, -20, -30, -40];

type MatchResultPayload = {
  lobbyId: string;
  matchSeed: number;
  usedBotFill: boolean;
  humanCount: number;
  placement: number;
  damageDealt: number;
  heroName: string;
  turnsPlayed: number;
};

Deno.serve(async (req) => {
  if (req.method !== "POST") {
    return json({ error: "Method not allowed" }, 405);
  }

  const supabaseUrl = Deno.env.get("SUPABASE_URL");
  const supabaseAnonKey = Deno.env.get("SUPABASE_ANON_KEY");
  const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");

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

  const payload = await req.json() as MatchResultPayload;
  const { data: profile, error: profileError } = await admin
    .from("player_profiles")
    .select("*")
    .eq("id", userData.user.id)
    .single();

  if (profileError || !profile) {
    return json({ error: profileError?.message ?? "Profile not found" }, 404);
  }

  const mmrBefore = profile.mmr as number;
  const delta = calculateDelta(payload.placement, mmrBefore);
  const mmrAfter = Math.max(0, mmrBefore + delta);
  const won = payload.placement === 1;
  const top4 = payload.placement <= 4;
  const currentWinStreak = won ? (profile.current_win_streak as number) + 1 : 0;
  const bestWinStreak = Math.max(profile.best_win_streak as number, currentWinStreak);

  const updatedProfile = {
    mmr: mmrAfter,
    highest_mmr: Math.max(profile.highest_mmr as number, mmrAfter),
    rated_games_played: (profile.rated_games_played as number) + 1,
    wins: (profile.wins as number) + (won ? 1 : 0),
    losses: (profile.losses as number) + (payload.placement >= 5 ? 1 : 0),
    top4_finishes: (profile.top4_finishes as number) + (top4 ? 1 : 0),
    current_win_streak: currentWinStreak,
    best_win_streak: bestWinStreak,
    total_damage_dealt: (profile.total_damage_dealt as number) + payload.damageDealt,
  };

  const { error: updateError } = await admin
    .from("player_profiles")
    .update(updatedProfile)
    .eq("id", userData.user.id);

  if (updateError) {
    return json({ error: updateError.message }, 500);
  }

  const { data: matchRow } = await admin
    .from("matches")
    .select("id")
    .eq("lobby_id", payload.lobbyId)
    .maybeSingle();

  let historyId: string | null = null;
  if (matchRow?.id) {
    const { data: history } = await admin
      .from("match_history")
      .insert({
        match_id: matchRow.id,
        lobby_id: payload.lobbyId,
        match_seed: payload.matchSeed,
        used_bot_fill: payload.usedBotFill,
        human_count: payload.humanCount,
        ended_at: new Date().toISOString(),
      })
      .select("id")
      .single();

    historyId = history?.id ?? null;

    if (historyId) {
      await admin.from("match_participants").insert({
        match_history_id: historyId,
        slot_index: 0,
        player_id: userData.user.id,
        is_bot: false,
        display_name: profile.display_name,
        placement: payload.placement,
        mmr_before: mmrBefore,
        mmr_after: mmrAfter,
        mmr_delta: delta,
        damage_dealt: payload.damageDealt,
        hero_name: payload.heroName,
      });
    }

    await admin.from("matches").update({ status: "completed", ended_at: new Date().toISOString() }).eq("id", matchRow.id);
  }

  const unlocked = await unlockAchievements(admin, userData.user.id, {
    ...profile,
    ...updatedProfile,
  });

  return json({
    mmrBefore,
    mmrAfter,
    mmrDelta: delta,
    profile: {
      ...profile,
      ...updatedProfile,
      playerId: userData.user.id,
      email: userData.user.email,
    },
    unlockedAchievements: unlocked,
  });
});

function calculateDelta(placement: number, currentMmr: number): number {
  const index = Math.min(Math.max(placement, 1), PLACEMENT_DELTA.length - 1);
  const baseDelta = PLACEMENT_DELTA[index];
  const mmrFactor = currentMmr < 1200 ? 1.2 : currentMmr > 1800 ? 0.85 : 1;
  return Math.round(baseDelta * mmrFactor);
}

async function unlockAchievements(
  admin: ReturnType<typeof createClient>,
  playerId: string,
  profile: Record<string, unknown>,
) {
  const candidates: Array<{ id: string; condition: boolean }> = [
    { id: "first_rated_game", condition: (profile.rated_games_played as number) >= 1 },
    { id: "first_win", condition: (profile.wins as number) >= 1 },
    { id: "win_streak_3", condition: (profile.current_win_streak as number) >= 3 },
    { id: "win_streak_5", condition: (profile.current_win_streak as number) >= 5 },
  ];

  const { data: existing } = await admin
    .from("player_achievements")
    .select("achievement_id")
    .eq("player_id", playerId);

  const owned = new Set((existing ?? []).map((row) => row.achievement_id as string));
  const unlocked: string[] = [];

  for (const achievement of candidates) {
    if (!achievement.condition || owned.has(achievement.id)) {
      continue;
    }

    const { error } = await admin.from("player_achievements").insert({
      player_id: playerId,
      achievement_id: achievement.id,
    });

    if (!error) {
      unlocked.push(achievement.id);
    }
  }

  return unlocked;
}

function json(payload: unknown, status = 200) {
  return new Response(JSON.stringify(payload), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}