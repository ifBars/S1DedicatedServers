import { describe, expect, test } from "bun:test";
import worker from "./index";
import { ACTIVE_SERVER_PREFIX, type ActiveServer, type ServerListResponse } from "./contracts";

describe("heartbeat authentication", () => {
  test("does not remove existing presence when authentication fails", async () => {
    const listingId = "00000000-0000-0000-0000-000000000001";
    const activeKey = `${ACTIVE_SERVER_PREFIX}${listingId}`;
    const cachedKeys = new Set([activeKey]);
    const env = {
      PORTAL_ORIGIN: "http://127.0.0.1:4173",
      HEARTBEAT_RATE_LIMITER: {
        limit: async () => ({ success: true }),
      },
      DB: {
        prepare: () => ({
          bind: () => ({
            first: async () => null,
          }),
        }),
      },
      SERVER_CACHE: {
        delete: async (key: string) => {
          cachedKeys.delete(key);
        },
      },
    } as unknown as Env;
    const executionContext = {
      waitUntil: () => undefined,
      passThroughOnException: () => undefined,
    } as unknown as ExecutionContext;
    const request = new Request(`http://127.0.0.1:8787/api/v2/listings/${listingId}/heartbeat`, {
      method: "PUT",
      headers: {
        Authorization: "Bearer invalid-secret",
        "CF-Connecting-IP": "203.0.113.10",
      },
      body: JSON.stringify({}),
    });

    const workerRequest = request as unknown as Parameters<typeof worker.fetch>[0];
    const response = await worker.fetch(workerRequest, env, executionContext);

    expect(response.status).toBe(401);
    expect(cachedKeys.has(activeKey)).toBe(true);
  });
});

describe("public server search", () => {
  const now = Date.now();
  const servers: ActiveServer[] = [
    makeServer("00000000-0000-0000-0000-000000000001", "Alpha", "203.0.113.10", 7777, now),
    makeServer("00000000-0000-0000-0000-000000000002", "Other", "203.0.113.11", 7777, now),
    makeServer("00000000-0000-0000-0000-000000000003", "Alpha Two", "203.0.113.10", 8888, now),
  ];

  test("keeps the unfiltered list response unchanged", async () => {
    const response = await search(searchEnv(servers), "?limit=2");
    const body = await response.json() as ServerListResponse;
    expect(body.servers.map((server) => server.listingId)).toEqual([servers[0].listingId, servers[1].listingId]);
    expect(body.nextCursor).toBe("2");
  });

  test("finds a name match beyond the first KV page and preserves the cursor", async () => {
    const env = searchEnv(servers);
    const first = await search(env, "?name=ALPHA&limit=1");
    expect(first.status).toBe(200);
    const firstPage = await first.json() as ServerListResponse;
    expect(firstPage.servers.map((server) => server.listingId)).toEqual([servers[0].listingId]);
    expect(firstPage.nextCursor).toBe("1");

    const second = await search(env, `?name=ALPHA&limit=1&cursor=${firstPage.nextCursor}`);
    const secondPage = await second.json() as ServerListResponse;
    expect(secondPage.servers.map((server) => server.listingId)).toEqual([servers[2].listingId]);
    expect(secondPage.nextCursor).toBeUndefined();
  });

  test("matches the advertised host and port together", async () => {
    const response = await search(searchEnv(servers), "?host=203.0.113.10&port=8888&limit=1");
    const body = await response.json() as ServerListResponse;
    expect(body.servers.map((server) => server.listingId)).toEqual([servers[2].listingId]);
  });

  test("returns a cursor when the search scan budget ends before a match", async () => {
    const manyServers = Array.from({ length: 21 }, (_, index) =>
      makeServer(`00000000-0000-0000-0000-${String(index).padStart(12, "0")}`, index === 20 ? "Target" : "Other", "203.0.113.10", 7777, now),
    );
    const env = searchEnv(manyServers);
    const first = await search(env, "?name=target&limit=1");
    const firstPage = await first.json() as ServerListResponse;
    expect(firstPage.servers).toEqual([]);
    expect(firstPage.nextCursor).toBe("20");

    const second = await search(env, `?name=target&limit=1&cursor=${firstPage.nextCursor}`);
    const secondPage = await second.json() as ServerListResponse;
    expect(secondPage.servers.map((server) => server.serverName)).toEqual(["Target"]);
    expect(secondPage.nextCursor).toBeUndefined();
  });

  test("excludes inactive listings and expired presence", async () => {
    const expired = makeServer("00000000-0000-0000-0000-000000000004", "Alpha Old", "203.0.113.12", 7777, now - 16 * 60 * 1000);
    const response = await search(searchEnv([...servers, expired], new Set([servers[0].listingId])), "?name=alpha");
    const body = await response.json() as ServerListResponse;
    expect(body.servers.map((server) => server.listingId)).toEqual([servers[0].listingId]);
  });

  test("rejects incomplete or invalid address filters", async () => {
    for (const query of ["?host=203.0.113.10", "?port=7777", "?host=203.0.113.10&port=0", "?host=203.0.113.10&port=1e3", "?name="]) {
      const response = await search(searchEnv(servers), query);
      expect(response.status).toBe(400);
      expect((await response.json() as { error: string }).error).toBe("INVALID_SEARCH");
    }
  });
});

function makeServer(listingId: string, serverName: string, host: string, port: number, lastHeartbeat: number): ActiveServer {
  return {
    listingId,
    protocolVersion: 2,
    serverName,
    serverDescription: "",
    currentPlayers: 1,
    maxPlayers: 10,
    port,
    host,
    passwordProtected: false,
    gameVersion: "1.0",
    modVersion: "1.0",
    lastHeartbeat,
  };
}

function searchEnv(servers: ActiveServer[], activeIds = new Set(servers.map((server) => server.listingId))): Env {
  return {
    PORTAL_ORIGIN: "http://127.0.0.1:4173",
    LIST_RATE_LIMITER: { limit: async () => ({ success: true }) },
    SERVER_CACHE: {
      list: async ({ limit, cursor }: { limit: number; cursor?: string }) => {
        const start = Number(cursor ?? "0");
        const end = Math.min(start + limit, servers.length);
        return {
          keys: servers.slice(start, end).map((metadata) => ({ metadata })),
          list_complete: end === servers.length,
          cursor: end === servers.length ? undefined : String(end),
        };
      },
    },
    DB: {
      prepare: () => ({
        bind: (...ids: string[]) => ({
          all: async () => ({ results: ids.filter((id) => activeIds.has(id)).map((id) => ({ id })) }),
        }),
      }),
    },
  } as unknown as Env;
}

async function search(env: Env, query: string): Promise<Response> {
  const request = new Request(`http://127.0.0.1:8787/api/v2/servers${query}`, {
    headers: { "CF-Connecting-IP": "198.51.100.5" },
  });
  const context = { waitUntil: () => undefined, passThroughOnException: () => undefined } as unknown as ExecutionContext;
  return worker.fetch(request as Parameters<typeof worker.fetch>[0], env, context);
}
