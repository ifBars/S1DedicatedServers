import { describe, expect, test } from "bun:test";
import worker from "./index";
import { ACTIVE_SERVER_PREFIX } from "./contracts";

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
