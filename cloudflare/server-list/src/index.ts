import {
  ACTIVE_SERVER_PREFIX,
  ACTIVE_SERVER_TTL_SECONDS,
  API_VERSION,
  MAX_REQUEST_BYTES,
  type ActiveServer,
  type ActiveListingIdRecord,
  type ErrorResponse,
  type ListingRecord,
  type ServerListResponse,
} from "./contracts";
import { getConnectingIp, readBearerSecret, sha256Hex } from "./security";
import { isPortalPath, routePortal } from "./portal";
import { isKvMetadataWithinLimit, validateHeartbeat } from "./validation";

const JSON_HEADERS = { "Content-Type": "application/json; charset=utf-8" };
const PERSIST_LAST_SEEN_INTERVAL_MS = 15 * 60 * 1000;
const MAX_SEARCH_SCANNED = 1000;
const MAX_SEARCH_PAGES = 20;

export default {
  async fetch(request, env, ctx): Promise<Response> {
    try {
      return await route(request, env, ctx);
    } catch (error) {
      if (error instanceof RequestBodyError) {
        return errorResponse(error.status, error.code, error.message);
      }

      console.error(JSON.stringify({ event: "request_failed", error: errorMessage(error) }));
      return errorResponse(500, "INTERNAL_ERROR", "The server-list service could not process the request.");
    }
  },
} satisfies ExportedHandler<Env>;

async function route(request: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
  const url = new URL(request.url);

  if (!isHttpsRequest(request, url, env)) {
    return errorResponse(400, "HTTPS_REQUIRED", "This endpoint requires HTTPS.");
  }

  if (request.method === "GET" && url.pathname === "/health") {
    return json({ status: "ok", protocolVersion: API_VERSION });
  }

  if (isPortalPath(url.pathname)) {
    return routePortal(request, url, env);
  }

  if (request.method === "POST" && url.pathname === "/api/v2/listings") {
    return errorResponse(403, "PORTAL_REQUIRED", "Create listing credentials at https://s1servers.com/server-portal.");
  }

  if (request.method === "GET" && url.pathname === "/api/v2/servers") {
    return listServers(request, url, env);
  }

  const heartbeatMatch = url.pathname.match(/^\/api\/v2\/listings\/([0-9a-f-]{36})\/heartbeat$/i);
  if (request.method === "PUT" && heartbeatMatch) {
    return heartbeat(request, heartbeatMatch[1], env, ctx);
  }

  const presenceMatch = url.pathname.match(/^\/api\/v2\/listings\/([0-9a-f-]{36})\/presence$/i);
  if (request.method === "DELETE" && presenceMatch) {
    return removePresence(request, presenceMatch[1], env);
  }

  return errorResponse(404, "NOT_FOUND", "Endpoint not found.");
}

function isHttpsRequest(request: Request, url: URL, env: Env): boolean {
  if (url.protocol === "https:" || /"scheme"\s*:\s*"https"/i.test(request.headers.get("CF-Visitor") ?? "")) {
    return true;
  }

  const portalOrigin = new URL(env.PORTAL_ORIGIN);
  return portalOrigin.protocol === "http:" && (portalOrigin.hostname === "localhost" || portalOrigin.hostname === "127.0.0.1");
}

async function heartbeat(
  request: Request,
  listingId: string,
  env: Env,
  ctx: ExecutionContext,
): Promise<Response> {
  const sourceIp = getConnectingIp(request);
  if (!sourceIp) {
    return errorResponse(400, "MISSING_SOURCE_IP", "Cloudflare source address is unavailable.");
  }

  const rateLimit = await env.HEARTBEAT_RATE_LIMITER.limit({ key: `${sourceIp}:${listingId}` });
  if (!rateLimit.success) {
    return errorResponse(429, "RATE_LIMITED", "Heartbeat rate limit exceeded.");
  }

  const listing = await authenticateListing(request, listingId, env.DB);
  if (!listing) {
    return errorResponse(401, "UNAUTHORIZED", "Listing credentials are invalid or inactive.");
  }

  const body = await readJson(request);
  const validation = validateHeartbeat(body);
  if (!validation.success) {
    return errorResponse(400, "INVALID_HEARTBEAT", validation.message);
  }

  const now = Date.now();
  const activeServer: ActiveServer = {
    listingId,
    ...validation.server,
    host: sourceIp,
    lastHeartbeat: now,
  };
  const serializedActiveServer = JSON.stringify(activeServer);
  if (!isKvMetadataWithinLimit(serializedActiveServer)) {
    return errorResponse(400, "INVALID_HEARTBEAT", "Heartbeat metadata exceeds the directory storage limit.");
  }

  await env.SERVER_CACHE.put(`${ACTIVE_SERVER_PREFIX}${listingId}`, serializedActiveServer, {
    expirationTtl: ACTIVE_SERVER_TTL_SECONDS,
    metadata: activeServer,
  });

  if (listing.last_persisted_at === null || now - listing.last_persisted_at >= PERSIST_LAST_SEEN_INTERVAL_MS) {
    ctx.waitUntil(
      env.DB.prepare(
        `UPDATE server_listings_v2
         SET last_seen = ?, last_persisted_at = ?, last_ip = ?, updated_at = ?
         WHERE id = ? AND state = 'active'`,
      )
        .bind(now, now, sourceIp, now, listingId)
        .run(),
    );
  }

  return json({ success: true, expiresInSeconds: ACTIVE_SERVER_TTL_SECONDS });
}

async function removePresence(request: Request, listingId: string, env: Env): Promise<Response> {
  const listing = await authenticateListing(request, listingId, env.DB);
  if (!listing) {
    return errorResponse(401, "UNAUTHORIZED", "Listing credentials are invalid or inactive.");
  }

  await env.SERVER_CACHE.delete(`${ACTIVE_SERVER_PREFIX}${listingId}`);
  return json({ success: true });
}

async function listServers(request: Request, url: URL, env: Env): Promise<Response> {
  const sourceIp = getConnectingIp(request);
  if (!sourceIp) {
    return errorResponse(400, "MISSING_SOURCE_IP", "Cloudflare source address is unavailable.");
  }

  const rateLimit = await env.LIST_RATE_LIMITER.limit({ key: sourceIp });
  if (!rateLimit.success) {
    return errorResponse(429, "RATE_LIMITED", "Server-list request rate limit exceeded.");
  }

  const limit = parseLimit(url.searchParams.get("limit"));
  const cursor = url.searchParams.get("cursor") ?? undefined;
  if (cursor && cursor.length > 2048) {
    return errorResponse(400, "INVALID_CURSOR", "Cursor is too long.");
  }

  const name = url.searchParams.get("name")?.trim();
  const host = url.searchParams.get("host")?.trim();
  const portText = url.searchParams.get("port");
  if (name !== undefined && (name.length === 0 || name.length > 100)) {
    return errorResponse(400, "INVALID_SEARCH", "Name must be between 1 and 100 characters.");
  }
  if ((host === undefined) !== (portText === null) || (host !== undefined && (host.length === 0 || host.length > 45))) {
    return errorResponse(400, "INVALID_SEARCH", "Host and port must be provided together; host must be 1 to 45 characters.");
  }
  if (portText !== null && !/^[1-9]\d{0,4}$/.test(portText)) {
    return errorResponse(400, "INVALID_SEARCH", "Port must be an integer between 1 and 65535.");
  }
  const port = portText === null ? undefined : Number(portText);
  if (port !== undefined && port > 65535) {
    return errorResponse(400, "INVALID_SEARCH", "Port must be an integer between 1 and 65535.");
  }

  if (name !== undefined || host !== undefined) {
    return searchServers(env, { name, host, port }, limit, cursor);
  }

  const result = await env.SERVER_CACHE.list<ActiveServer>({
    prefix: ACTIVE_SERVER_PREFIX,
    limit,
    cursor,
  });
  const oldestAcceptedHeartbeat = Date.now() - ACTIVE_SERVER_TTL_SECONDS * 1000;
  const candidates = result.keys
    .map((key) => key.metadata)
    .filter((server): server is ActiveServer =>
      server !== null &&
      server !== undefined &&
      server.protocolVersion === API_VERSION &&
      server.lastHeartbeat >= oldestAcceptedHeartbeat,
    );
  const activeListingIds = await getActiveListingIds(candidates, env.DB);
  const servers = candidates.filter((server) => activeListingIds.has(server.listingId));

  const response: ServerListResponse = {
    success: true,
    servers,
    ...(!result.list_complete && result.cursor ? { nextCursor: result.cursor } : {}),
  };
  return json(response, 200, { "Cache-Control": "public, max-age=10" });
}

async function searchServers(
  env: Env,
  filters: { name?: string; host?: string; port?: number },
  limit: number,
  cursor?: string,
): Promise<Response> {
  const servers: ActiveServer[] = [];
  const oldestAcceptedHeartbeat = Date.now() - ACTIVE_SERVER_TTL_SECONDS * 1000;
  let scanned = 0;
  let pages = 0;
  let nextCursor = cursor;
  let complete = false;
  const nameSearch = filters.name?.toLowerCase();
  const hostSearch = filters.host?.toLowerCase();

  while (!complete && servers.length < limit && scanned < MAX_SEARCH_SCANNED && pages < MAX_SEARCH_PAGES) {
    const result = await env.SERVER_CACHE.list<ActiveServer>({
      prefix: ACTIVE_SERVER_PREFIX,
      limit: Math.min(100, limit - servers.length, MAX_SEARCH_SCANNED - scanned),
      cursor: nextCursor,
    });
    pages++;
    scanned += result.keys.length;
    complete = result.list_complete;
    nextCursor = result.list_complete ? undefined : result.cursor;

    const candidates = result.keys
      .map((key) => key.metadata)
      .filter((server): server is ActiveServer =>
        server !== null &&
        server !== undefined &&
        server.protocolVersion === API_VERSION &&
        server.lastHeartbeat >= oldestAcceptedHeartbeat &&
        (nameSearch === undefined || server.serverName.toLowerCase().includes(nameSearch)) &&
        (hostSearch === undefined || (server.host.toLowerCase() === hostSearch && server.port === filters.port)),
      );
    const activeListingIds = await getActiveListingIds(candidates, env.DB);
    servers.push(...candidates.filter((server) => activeListingIds.has(server.listingId)));

    if (!complete && !nextCursor) {
      throw new Error("Server-list pagination did not return a continuation cursor.");
    }
  }

  const response: ServerListResponse = {
    success: true,
    servers,
    ...(!complete && nextCursor ? { nextCursor } : {}),
  };
  return json(response, 200, { "Cache-Control": "public, max-age=10" });
}

async function getActiveListingIds(candidates: ActiveServer[], db: D1Database): Promise<Set<string>> {
  if (candidates.length === 0) {
    return new Set<string>();
  }

  const placeholders = candidates.map(() => "?").join(", ");
  const active = await db
    .prepare(
      `SELECT id FROM server_listings_v2
       WHERE state = 'active' AND operator_id IS NOT NULL AND id IN (${placeholders})`,
    )
    .bind(...candidates.map((candidate) => candidate.listingId))
    .all<ActiveListingIdRecord>();
  return new Set(active.results.map((record) => record.id));
}

async function authenticateListing(
  request: Request,
  listingId: string,
  db: D1Database,
): Promise<ListingRecord | null> {
  const secret = readBearerSecret(request);
  if (!secret) {
    return null;
  }

  const secretHash = await sha256Hex(secret);
  return db
    .prepare(
      `SELECT state, last_persisted_at
       FROM server_listings_v2
       WHERE id = ? AND secret_hash = ? AND state = 'active' AND operator_id IS NOT NULL`,
    )
    .bind(listingId, secretHash)
    .first<ListingRecord>();
}

async function readJson(request: Request): Promise<unknown> {
  const declaredLength = Number.parseInt(request.headers.get("Content-Length") ?? "0", 10);
  if (declaredLength > MAX_REQUEST_BYTES) {
    throw new RequestBodyError(413, "REQUEST_TOO_LARGE", "Request body exceeds 16 KiB.");
  }

  if (!request.body) {
    throw new RequestBodyError(400, "INVALID_JSON", "A JSON request body is required.");
  }

  const reader = request.body.getReader();
  const decoder = new TextDecoder();
  let text = "";
  let bytesRead = 0;
  while (true) {
    const { done, value } = await reader.read();
    if (done) {
      break;
    }

    bytesRead += value.byteLength;
    if (bytesRead > MAX_REQUEST_BYTES) {
      await reader.cancel();
      throw new RequestBodyError(413, "REQUEST_TOO_LARGE", "Request body exceeds 16 KiB.");
    }

    text += decoder.decode(value, { stream: true });
  }
  text += decoder.decode();

  try {
    return JSON.parse(text) as unknown;
  } catch {
    throw new RequestBodyError(400, "INVALID_JSON", "Request body is not valid JSON.");
  }
}

function parseLimit(value: string | null): number {
  const parsed = Number.parseInt(value ?? "50", 10);
  return Number.isFinite(parsed) ? Math.max(1, Math.min(parsed, 100)) : 50;
}

function json<T>(body: T, status = 200, extraHeaders: HeadersInit = {}): Response {
  return Response.json(body, { status, headers: { ...JSON_HEADERS, ...extraHeaders } });
}

function errorResponse(status: number, error: string, message: string): Response {
  return json<ErrorResponse>({ success: false, error, message }, status);
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

class RequestBodyError extends Error {
  public constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
  ) {
    super(message);
  }
}
