import { createClient } from "https://esm.sh/@supabase/supabase-js@2.49.1";

const UNITY_PROJECT_ID =
  Deno.env.get("UNITY_PROJECT_ID") ?? "fbdba5bf-6cda-4d17-a13a-2eb5609c2dff";
const BRIDGE_SECRET = Deno.env.get("UGS_BRIDGE_SECRET") ?? "";

type JsonRecord = Record<string, unknown>;

function json(payload: JsonRecord, status = 200): Response {
  return new Response(JSON.stringify(payload), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function sanitizePlayerId(playerId: string): string {
  return playerId.replace(/[^a-zA-Z0-9]/g, "").toLowerCase();
}

async function derivePassword(playerId: string, secret: string): Promise<string> {
  const data = new TextEncoder().encode(`${playerId}:${secret}`);
  const hash = await crypto.subtle.digest("SHA-256", data);
  const hex = Array.from(new Uint8Array(hash))
    .map((value) => value.toString(16).padStart(2, "0"))
    .join("");
  return `${hex.slice(0, 22)}Aa1!`;
}

async function verifyUgsToken(idToken: string, playerId: string): Promise<boolean> {
  const response = await fetch(
    `https://player-auth.services.api.unity.com/v1/users/${encodeURIComponent(playerId)}`,
    {
      headers: {
        ProjectId: UNITY_PROJECT_ID,
        Authorization: `Bearer ${idToken}`,
      },
    },
  );
  return response.ok;
}

Deno.serve(async (req) => {
  if (req.method !== "POST") {
    return json({ error: "Method not allowed" }, 405);
  }

  if (!BRIDGE_SECRET) {
    return json({ error: "UGS bridge is not configured on the server." }, 500);
  }

  const authHeader = req.headers.get("Authorization") ?? "";
  const idToken = authHeader.startsWith("Bearer ") ? authHeader.slice(7).trim() : "";
  if (!idToken) {
    return json({ error: "Missing Unity access token." }, 401);
  }

  const body = await req.json().catch(() => ({}));
  const ugsPlayerId = String(body.ugsPlayerId ?? "").trim();
  const requestedDisplayName = String(body.displayName ?? "").trim();
  const displayName = requestedDisplayName || "Dreamer";

  if (!ugsPlayerId) {
    return json({ error: "Missing Unity player id." }, 400);
  }

  if (!(await verifyUgsToken(idToken, ugsPlayerId))) {
    return json({ error: "Invalid Unity session." }, 401);
  }

  const supabaseUrl = Deno.env.get("SUPABASE_URL");
  const anonKey = Deno.env.get("SUPABASE_ANON_KEY");
  const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");

  if (!supabaseUrl || !anonKey || !serviceRoleKey) {
    return json({ error: "Server misconfigured." }, 500);
  }

  const email = `ugs+${sanitizePlayerId(ugsPlayerId)}@dreamgate.auth`;
  const password = await derivePassword(ugsPlayerId, BRIDGE_SECRET);
  const admin = createClient(supabaseUrl, serviceRoleKey);
  const userClient = createClient(supabaseUrl, anonKey);

  let signIn = await userClient.auth.signInWithPassword({ email, password });
  if (signIn.error) {
    const createResult = await admin.auth.admin.createUser({
      email,
      password,
      email_confirm: true,
      user_metadata: {
        display_name: displayName,
        ugs_player_id: ugsPlayerId,
      },
    });

    if (createResult.error) {
      return json({ error: createResult.error.message }, 400);
    }

    signIn = await userClient.auth.signInWithPassword({ email, password });
    if (signIn.error || !signIn.data.session) {
      return json({ error: signIn.error?.message ?? "Could not start cloud session." }, 400);
    }
  }

  const session = signIn.data.session;
  const userId = signIn.data.user?.id ?? "";
  if (!session || !userId) {
    return json({ error: "Could not start cloud session." }, 400);
  }

  if (
    requestedDisplayName.length >= 2 &&
    !requestedDisplayName.toLowerCase().startsWith("ugs+")
  ) {
    await admin
      .from("player_profiles")
      .update({ display_name: requestedDisplayName })
      .eq("id", userId);
  }

  return json({
    access_token: session.access_token,
    refresh_token: session.refresh_token,
    user: signIn.data.user,
  });
});