import { ACTIVE_SERVER_PREFIX, type ErrorResponse } from "./contracts";
import { generateSecret, getConnectingIp, secureEqual, sha256Hex } from "./security";

const SESSION_COOKIE = "__Host-s1ds_portal";
const OAUTH_STATE_COOKIE = "__Host-s1ds_oauth_state";
const SESSION_LIFETIME_SECONDS = 7 * 24 * 60 * 60;
const OAUTH_STATE_LIFETIME_SECONDS = 10 * 60;
const MAX_ACTIVE_LISTINGS = 10;
const STEAM_OPENID_ENDPOINT = "https://steamcommunity.com/openid/login";

// Wrangler generates all checked-in bindings. Deployed secrets are intentionally absent from
// wrangler.jsonc, so this narrow intersection describes the one runtime-only secret.
type PortalEnv = Env & {
  readonly DISCORD_CLIENT_ID?: string;
  readonly DISCORD_CLIENT_SECRET?: string;
};

type Provider = "discord" | "steam";

interface PortalSessionRow {
  operator_id: string;
  csrf_token: string;
  expires_at: number;
}

interface IdentityRow {
  provider: Provider;
  subject: string;
  display_name: string;
}

interface ListingRow {
  id: string;
  label: string;
  state: "active" | "revoked" | "banned";
  created_at: number;
  updated_at: number;
  last_seen: number | null;
}

interface IdentityOwnerRow {
  operator_id: string;
}

export function isPortalPath(pathname: string): boolean {
  return pathname.startsWith("/api/v2/portal/");
}

export async function routePortal(request: Request, url: URL, env: PortalEnv): Promise<Response> {
  if (request.method === "OPTIONS") {
    return withCors(request, env, new Response(null, { status: 204 }));
  }

  const shouldRateLimit = request.method !== "GET" || url.pathname.endsWith("/start");
  if (shouldRateLimit) {
    const sourceIp = getConnectingIp(request);
    if (!sourceIp) {
      return withCors(request, env, portalError(400, "MISSING_SOURCE_IP", "Cloudflare source address is unavailable."));
    }
    const rateLimit = await env.REGISTRATION_RATE_LIMITER.limit({ key: `${sourceIp}:${url.pathname}` });
    if (!rateLimit.success) {
      return withCors(request, env, portalError(429, "RATE_LIMITED", "Too many portal actions from this address."));
    }
  }

  let response: Response;
  if (request.method === "GET" && url.pathname === "/api/v2/portal/providers") {
    response = portalJson({
      success: true,
      providers: { discord: Boolean(env.DISCORD_CLIENT_ID && env.DISCORD_CLIENT_SECRET), steam: true },
    });
  } else if (request.method === "GET" && url.pathname === "/api/v2/portal/auth/discord/start") {
    response = await startDiscord(url, env);
  } else if (request.method === "GET" && url.pathname === "/api/v2/portal/auth/discord/callback") {
    response = await finishDiscord(request, url, env);
  } else if (request.method === "GET" && url.pathname === "/api/v2/portal/auth/steam/start") {
    response = await startSteam(url, env);
  } else if (request.method === "GET" && url.pathname === "/api/v2/portal/auth/steam/callback") {
    response = await finishSteam(request, url, env);
  } else if (request.method === "GET" && url.pathname === "/api/v2/portal/me") {
    response = await getPortalAccount(request, env);
  } else if (request.method === "POST" && url.pathname === "/api/v2/portal/logout") {
    response = await logout(request, env);
  } else if (request.method === "GET" && url.pathname === "/api/v2/portal/listings") {
    response = await listOperatorListings(request, env);
  } else if (request.method === "POST" && url.pathname === "/api/v2/portal/listings") {
    response = await createOperatorListing(request, env);
  } else {
    const listingMatch = url.pathname.match(/^\/api\/v2\/portal\/listings\/([0-9a-f-]{36})$/i);
    const rotateMatch = url.pathname.match(/^\/api\/v2\/portal\/listings\/([0-9a-f-]{36})\/rotate$/i);
    if (request.method === "DELETE" && listingMatch) {
      response = await revokeOperatorListing(request, listingMatch[1], env);
    } else if (request.method === "POST" && rotateMatch) {
      response = await rotateOperatorListing(request, rotateMatch[1], env);
    } else {
      response = portalError(404, "NOT_FOUND", "Portal endpoint not found.");
    }
  }

  return withCors(request, env, response);
}

async function startDiscord(url: URL, env: PortalEnv): Promise<Response> {
  if (!env.DISCORD_CLIENT_ID || !env.DISCORD_CLIENT_SECRET) {
    return portalRedirect(env, "provider_unavailable");
  }

  const state = await createOauthState("discord", env);
  const callback = `${url.origin}/api/v2/portal/auth/discord/callback`;
  const authorize = new URL("https://discord.com/oauth2/authorize");
  authorize.searchParams.set("client_id", env.DISCORD_CLIENT_ID);
  authorize.searchParams.set("response_type", "code");
  authorize.searchParams.set("redirect_uri", callback);
  authorize.searchParams.set("scope", "identify");
  authorize.searchParams.set("state", state);
  authorize.searchParams.set("prompt", "consent");
  return redirectWithOauthCookie(authorize.toString(), state);
}

async function finishDiscord(request: Request, url: URL, env: PortalEnv): Promise<Response> {
  if (!env.DISCORD_CLIENT_ID || !env.DISCORD_CLIENT_SECRET) {
    return portalRedirect(env, "provider_unavailable");
  }

  const state = url.searchParams.get("state");
  if (!(await consumeOauthState(request, state, "discord", env))) {
    return portalRedirect(env, "invalid_state");
  }

  const code = url.searchParams.get("code");
  if (!code || code.length > 2048) {
    return portalRedirect(env, "missing_code");
  }

  const callback = `${url.origin}/api/v2/portal/auth/discord/callback`;
  const tokenResponse = await fetch("https://discord.com/api/v10/oauth2/token", {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      client_id: env.DISCORD_CLIENT_ID,
      client_secret: env.DISCORD_CLIENT_SECRET,
      grant_type: "authorization_code",
      code,
      redirect_uri: callback,
    }),
  });
  if (!tokenResponse.ok) {
    return portalRedirect(env, "provider_exchange_failed");
  }

  const token = await tokenResponse.json<unknown>();
  const accessToken = readStringField(token, "access_token", 4096);
  if (!accessToken) {
    return portalRedirect(env, "provider_exchange_failed");
  }

  const userResponse = await fetch("https://discord.com/api/v10/users/@me", {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!userResponse.ok) {
    return portalRedirect(env, "provider_identity_failed");
  }

  const user = await userResponse.json<unknown>();
  const subject = readStringField(user, "id", 32);
  const displayName = readStringField(user, "global_name", 100) ?? readStringField(user, "username", 100);
  if (!subject || !/^\d{16,24}$/.test(subject) || !displayName) {
    return portalRedirect(env, "provider_identity_failed");
  }

  return completeLogin("discord", subject, displayName, env);
}

async function startSteam(url: URL, env: PortalEnv): Promise<Response> {
  const state = await createOauthState("steam", env);
  const callback = new URL(`${url.origin}/api/v2/portal/auth/steam/callback`);
  callback.searchParams.set("state", state);
  const authorize = new URL(STEAM_OPENID_ENDPOINT);
  authorize.searchParams.set("openid.ns", "http://specs.openid.net/auth/2.0");
  authorize.searchParams.set("openid.mode", "checkid_setup");
  authorize.searchParams.set("openid.return_to", callback.toString());
  authorize.searchParams.set("openid.realm", `${url.origin}/`);
  authorize.searchParams.set("openid.identity", "http://specs.openid.net/auth/2.0/identifier_select");
  authorize.searchParams.set("openid.claimed_id", "http://specs.openid.net/auth/2.0/identifier_select");
  return redirectWithOauthCookie(authorize.toString(), state);
}

async function finishSteam(request: Request, url: URL, env: PortalEnv): Promise<Response> {
  const state = url.searchParams.get("state");
  if (!(await consumeOauthState(request, state, "steam", env))) {
    return portalRedirect(env, "invalid_state");
  }

  const expectedReturnTo = new URL(`${url.origin}/api/v2/portal/auth/steam/callback`);
  expectedReturnTo.searchParams.set("state", state!);
  if (
    url.searchParams.get("openid.ns") !== "http://specs.openid.net/auth/2.0" ||
    url.searchParams.get("openid.mode") !== "id_res" ||
    url.searchParams.get("openid.op_endpoint") !== STEAM_OPENID_ENDPOINT ||
    url.searchParams.get("openid.return_to") !== expectedReturnTo.toString()
  ) {
    return portalRedirect(env, "provider_identity_failed");
  }

  const verification = new URLSearchParams();
  for (const [key, value] of url.searchParams) {
    if (key.startsWith("openid.")) {
      verification.set(key, value);
    }
  }
  verification.set("openid.mode", "check_authentication");
  const verifyResponse = await fetch(STEAM_OPENID_ENDPOINT, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: verification,
  });
  const verifyBody = await verifyResponse.text();
  if (!verifyResponse.ok || !/(^|\n)is_valid:true(\r?$|\n)/m.test(verifyBody)) {
    return portalRedirect(env, "provider_identity_failed");
  }

  const claimedId = url.searchParams.get("openid.claimed_id") ?? "";
  if (url.searchParams.get("openid.identity") !== claimedId) {
    return portalRedirect(env, "provider_identity_failed");
  }
  const subject = /^https:\/\/steamcommunity\.com\/openid\/id\/(\d{17})$/.exec(claimedId)?.[1];
  if (!subject) {
    return portalRedirect(env, "provider_identity_failed");
  }

  return completeLogin("steam", subject, `Steam ${subject.slice(-6)}`, env);
}

async function completeLogin(provider: Provider, subject: string, displayName: string, env: PortalEnv): Promise<Response> {
  const now = Date.now();
  let owner = await env.DB.prepare(
    `SELECT i.operator_id
     FROM portal_identities i
     JOIN portal_operators o ON o.id = i.operator_id
     WHERE i.provider = ? AND i.subject = ? AND o.state = 'active'`,
  ).bind(provider, subject).first<IdentityOwnerRow>();
  if (owner) {
    await env.DB.prepare(
      "UPDATE portal_identities SET display_name = ?, last_login_at = ? WHERE provider = ? AND subject = ?",
    ).bind(displayName, now, provider, subject).run();
  } else {
    const newOperatorId = crypto.randomUUID();
    await env.DB.batch([
      env.DB.prepare(
        "INSERT INTO portal_operators (id, state, created_at, updated_at) VALUES (?, 'active', ?, ?)",
      ).bind(newOperatorId, now, now),
      env.DB.prepare(
        `INSERT OR IGNORE INTO portal_identities
         (provider, subject, operator_id, display_name, created_at, last_login_at)
         VALUES (?, ?, ?, ?, ?, ?)`,
      ).bind(provider, subject, newOperatorId, displayName, now, now),
    ]);
    owner = await env.DB.prepare(
      `SELECT i.operator_id
       FROM portal_identities i
       JOIN portal_operators o ON o.id = i.operator_id
       WHERE i.provider = ? AND i.subject = ? AND o.state = 'active'`,
    ).bind(provider, subject).first<IdentityOwnerRow>();
  }
  if (!owner) {
    return portalRedirect(env, "account_unavailable");
  }

  const sessionToken = generateSecret();
  const csrfToken = generateSecret();
  await env.DB.prepare(
    "INSERT INTO portal_sessions (token_hash, operator_id, csrf_token, created_at, expires_at) VALUES (?, ?, ?, ?, ?)",
  ).bind(
    await sha256Hex(sessionToken),
    owner.operator_id,
    csrfToken,
    now,
    now + SESSION_LIFETIME_SECONDS * 1000,
  ).run();

  const response = portalRedirect(env, "signed_in");
  response.headers.append("Set-Cookie", sessionCookie(sessionToken));
  response.headers.append("Set-Cookie", clearOauthCookie());
  return response;
}

async function getPortalAccount(request: Request, env: PortalEnv): Promise<Response> {
  const session = await getSession(request, env);
  if (!session) {
    return portalError(401, "UNAUTHORIZED", "Sign in to manage public listings.");
  }

  const identities = await env.DB.prepare(
    "SELECT provider, subject, display_name FROM portal_identities WHERE operator_id = ? ORDER BY provider",
  ).bind(session.operator_id).all<IdentityRow>();
  return portalJson({
    success: true,
    operatorId: session.operator_id,
    csrfToken: session.csrf_token,
    identities: identities.results,
  });
}

async function logout(request: Request, env: PortalEnv): Promise<Response> {
  const session = await requireMutationSession(request, env);
  if (session instanceof Response) {
    return session;
  }

  const token = readCookie(request, SESSION_COOKIE);
  if (token) {
    await env.DB.prepare("DELETE FROM portal_sessions WHERE token_hash = ?").bind(await sha256Hex(token)).run();
  }
  const response = portalJson({ success: true });
  response.headers.append("Set-Cookie", clearSessionCookie());
  return response;
}

async function listOperatorListings(request: Request, env: PortalEnv): Promise<Response> {
  const session = await getSession(request, env);
  if (!session) {
    return portalError(401, "UNAUTHORIZED", "Sign in to manage public listings.");
  }

  const listings = await env.DB.prepare(
    `SELECT id, label, state, created_at, updated_at, last_seen
     FROM server_listings_v2 WHERE operator_id = ? ORDER BY created_at DESC`,
  ).bind(session.operator_id).all<ListingRow>();
  return portalJson({ success: true, listings: listings.results });
}

async function createOperatorListing(request: Request, env: PortalEnv): Promise<Response> {
  const session = await requireMutationSession(request, env);
  if (session instanceof Response) {
    return session;
  }

  const count = await env.DB.prepare(
    "SELECT COUNT(*) AS count FROM server_listings_v2 WHERE operator_id = ? AND state = 'active'",
  ).bind(session.operator_id).first<{ count: number }>();
  if ((count?.count ?? 0) >= MAX_ACTIVE_LISTINGS) {
    return portalError(409, "LISTING_LIMIT", `An operator may have at most ${MAX_ACTIVE_LISTINGS} active listings.`);
  }

  const body = await readSmallJson(request);
  const label = readStringField(body, "label", 80)?.trim();
  if (!label) {
    return portalError(400, "INVALID_LABEL", "A listing label is required.");
  }

  const listingId = crypto.randomUUID();
  const secret = generateSecret();
  const now = Date.now();
  await env.DB.prepare(
    `INSERT INTO server_listings_v2
     (id, secret_hash, state, created_at, updated_at, last_seen, last_persisted_at, last_ip, operator_id, label)
     VALUES (?, ?, 'active', ?, ?, NULL, NULL, NULL, ?, ?)`,
  ).bind(listingId, await sha256Hex(secret), now, now, session.operator_id, label).run();
  console.log(JSON.stringify({ event: "portal_listing_created", listingId, operatorId: session.operator_id }));
  return portalJson({ success: true, listingId, secret, label }, 201);
}

async function revokeOperatorListing(request: Request, listingId: string, env: PortalEnv): Promise<Response> {
  const session = await requireMutationSession(request, env);
  if (session instanceof Response) {
    return session;
  }

  const result = await env.DB.prepare(
    "UPDATE server_listings_v2 SET state = 'revoked', updated_at = ? WHERE id = ? AND operator_id = ? AND state != 'banned'",
  ).bind(Date.now(), listingId, session.operator_id).run();
  if (!result.meta.changes) {
    return portalError(404, "LISTING_NOT_FOUND", "Listing not found.");
  }

  await env.SERVER_CACHE.delete(`${ACTIVE_SERVER_PREFIX}${listingId}`);
  return portalJson({ success: true });
}

async function rotateOperatorListing(request: Request, listingId: string, env: PortalEnv): Promise<Response> {
  const session = await requireMutationSession(request, env);
  if (session instanceof Response) {
    return session;
  }

  const secret = generateSecret();
  const result = await env.DB.prepare(
    `UPDATE server_listings_v2
     SET secret_hash = ?, state = 'active', updated_at = ?, last_seen = NULL, last_persisted_at = NULL, last_ip = NULL
     WHERE id = ? AND operator_id = ? AND state != 'banned'`,
  ).bind(await sha256Hex(secret), Date.now(), listingId, session.operator_id).run();
  if (!result.meta.changes) {
    return portalError(404, "LISTING_NOT_FOUND", "Listing not found.");
  }

  await env.SERVER_CACHE.delete(`${ACTIVE_SERVER_PREFIX}${listingId}`);
  return portalJson({ success: true, listingId, secret });
}

async function requireMutationSession(request: Request, env: PortalEnv): Promise<PortalSessionRow | Response> {
  if (request.headers.get("Origin") !== env.PORTAL_ORIGIN) {
    return portalError(403, "INVALID_ORIGIN", "This request did not come from the server portal.");
  }
  const session = await getSession(request, env);
  if (!session) {
    return portalError(401, "UNAUTHORIZED", "Sign in to manage public listings.");
  }
  const csrf = request.headers.get("X-CSRF-Token");
  if (!csrf || !(await secureEqual(csrf, session.csrf_token))) {
    return portalError(403, "INVALID_CSRF", "The portal session could not be verified.");
  }
  return session;
}

async function getSession(request: Request, env: PortalEnv): Promise<PortalSessionRow | null> {
  const token = readCookie(request, SESSION_COOKIE);
  if (!token) {
    return null;
  }
  const now = Date.now();
  const session = await env.DB.prepare(
    `SELECT s.operator_id, s.csrf_token, s.expires_at
     FROM portal_sessions s
     JOIN portal_operators o ON o.id = s.operator_id
     WHERE s.token_hash = ? AND s.expires_at > ? AND o.state = 'active'`,
  ).bind(await sha256Hex(token), now).first<PortalSessionRow>();
  return session ?? null;
}

async function createOauthState(provider: Provider, env: PortalEnv): Promise<string> {
  const state = generateSecret();
  const now = Date.now();
  await env.DB.batch([
    env.DB.prepare("DELETE FROM portal_oauth_states WHERE expires_at <= ?").bind(now),
    env.DB.prepare("DELETE FROM portal_sessions WHERE expires_at <= ?").bind(now),
    env.DB.prepare(
      "INSERT INTO portal_oauth_states (state_hash, provider, created_at, expires_at) VALUES (?, ?, ?, ?)",
    ).bind(await sha256Hex(state), provider, now, now + OAUTH_STATE_LIFETIME_SECONDS * 1000),
  ]);
  return state;
}

async function consumeOauthState(
  request: Request,
  state: string | null,
  provider: Provider,
  env: PortalEnv,
): Promise<boolean> {
  const cookieState = readCookie(request, OAUTH_STATE_COOKIE);
  if (!state || !cookieState || !(await secureEqual(state, cookieState))) {
    return false;
  }
  const stateHash = await sha256Hex(state);
  const record = await env.DB.prepare(
    "SELECT state_hash FROM portal_oauth_states WHERE state_hash = ? AND provider = ? AND expires_at > ?",
  ).bind(stateHash, provider, Date.now()).first<{ state_hash: string }>();
  if (!record) {
    return false;
  }
  await env.DB.prepare("DELETE FROM portal_oauth_states WHERE state_hash = ?").bind(stateHash).run();
  return true;
}

function withCors(request: Request, env: PortalEnv, response: Response): Response {
  const origin = request.headers.get("Origin");
  if (origin !== env.PORTAL_ORIGIN) {
    return response;
  }
  const headers = new Headers(response.headers);
  headers.set("Access-Control-Allow-Origin", origin);
  headers.set("Access-Control-Allow-Credentials", "true");
  headers.set("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
  headers.set("Access-Control-Allow-Headers", "Content-Type, X-CSRF-Token");
  headers.set("Access-Control-Max-Age", "600");
  headers.append("Vary", "Origin");
  return new Response(response.body, { status: response.status, statusText: response.statusText, headers });
}

function portalRedirect(env: PortalEnv, status: string): Response {
  const redirect = new URL("/server-portal", env.PORTAL_ORIGIN);
  redirect.searchParams.set("auth", status);
  return new Response(null, { status: 302, headers: { Location: redirect.toString() } });
}

function redirectWithOauthCookie(location: string, state: string): Response {
  return new Response(null, {
    status: 302,
    headers: {
      Location: location,
      "Set-Cookie": `${OAUTH_STATE_COOKIE}=${state}; Path=/; Max-Age=${OAUTH_STATE_LIFETIME_SECONDS}; HttpOnly; Secure; SameSite=Lax`,
    },
  });
}

function sessionCookie(value: string): string {
  return `${SESSION_COOKIE}=${value}; Path=/; Max-Age=${SESSION_LIFETIME_SECONDS}; HttpOnly; Secure; SameSite=Lax`;
}

function clearSessionCookie(): string {
  return `${SESSION_COOKIE}=; Path=/; Max-Age=0; HttpOnly; Secure; SameSite=Lax`;
}

function clearOauthCookie(): string {
  return `${OAUTH_STATE_COOKIE}=; Path=/; Max-Age=0; HttpOnly; Secure; SameSite=Lax`;
}

function readCookie(request: Request, name: string): string | null {
  const cookie = request.headers.get("Cookie") ?? "";
  for (const part of cookie.split(";")) {
    const [key, ...value] = part.trim().split("=");
    if (key === name) {
      const joined = value.join("=");
      return joined.length >= 32 && joined.length <= 256 ? joined : null;
    }
  }
  return null;
}

async function readSmallJson(request: Request): Promise<unknown> {
  const maxBytes = 4096;
  const contentLength = Number.parseInt(request.headers.get("Content-Length") ?? "0", 10);
  if (contentLength > maxBytes || !request.body) {
    return null;
  }

  const reader = request.body.getReader();
  const decoder = new TextDecoder();
  let text = "";
  let bytesRead = 0;
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        break;
      }
      bytesRead += value.byteLength;
      if (bytesRead > maxBytes) {
        await reader.cancel();
        return null;
      }
      text += decoder.decode(value, { stream: true });
    }
    text += decoder.decode();
    return JSON.parse(text) as unknown;
  } catch {
    return null;
  }
}

function readStringField(value: unknown, key: string, maxLength: number): string | null {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return null;
  }
  const field = (value as Record<string, unknown>)[key];
  return typeof field === "string" && field.length > 0 && field.length <= maxLength ? field : null;
}

function portalJson<T>(body: T, status = 200): Response {
  return Response.json(body, {
    status,
    headers: {
      "Cache-Control": "no-store",
      "Content-Type": "application/json; charset=utf-8",
      "X-Content-Type-Options": "nosniff",
    },
  });
}

function portalError(status: number, error: string, message: string): Response {
  return portalJson<ErrorResponse>({ success: false, error, message }, status);
}
