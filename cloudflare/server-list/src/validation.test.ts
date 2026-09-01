import { describe, expect, test } from "bun:test";
import { ACTIVE_SERVER_TTL_SECONDS, type ActiveServer } from "./contracts";
import { isKvMetadataWithinLimit, validateHeartbeat } from "./validation";

const validHeartbeat = {
  protocolVersion: 2,
  serverName: "  Test Server  ",
  serverDescription: "Public test server",
  currentPlayers: 3,
  maxPlayers: 16,
  port: 38465,
  passwordProtected: false,
  gameVersion: "0.4.0",
  modVersion: "1.0.8",
};

describe("validateHeartbeat", () => {
  test("keeps presence for three five-minute heartbeat intervals", () => {
    expect(ACTIVE_SERVER_TTL_SECONDS).toBe(15 * 60);
  });

  test("normalizes a valid heartbeat", () => {
    const result = validateHeartbeat(validHeartbeat);

    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.server.serverName).toBe("Test Server");
      expect(result.server.port).toBe(38465);
    }
  });

  test("rejects incompatible protocol versions", () => {
    const result = validateHeartbeat({ ...validHeartbeat, protocolVersion: 1 });

    expect(result).toEqual({ success: false, message: "Unsupported protocol version. Expected 2." });
  });

  test("rejects impossible player counts", () => {
    const result = validateHeartbeat({ ...validHeartbeat, currentPlayers: 17 });

    expect(result).toEqual({
      success: false,
      message: "Current player count cannot exceed maximum player count.",
    });
  });

  test("rejects invalid ports", () => {
    const result = validateHeartbeat({ ...validHeartbeat, port: 0 });

    expect(result).toEqual({ success: false, message: "Port must be between 1 and 65535." });
  });

  test("rejects serialized metadata that exceeds the KV byte limit", () => {
    const oversizedHeartbeat = {
      ...validHeartbeat,
      serverName: "界".repeat(100),
      serverDescription: "界".repeat(280),
      gameVersion: "界".repeat(50),
      modVersion: "界".repeat(50),
    };
    const validation = validateHeartbeat(oversizedHeartbeat);
    expect(validation.success).toBe(true);
    if (!validation.success) {
      return;
    }

    const oversizedServer: ActiveServer = {
      listingId: "00000000-0000-0000-0000-000000000000",
      ...validation.server,
      host: "2001:db8:ffff:ffff:ffff:ffff:ffff:ffff",
      lastHeartbeat: 1_785_562_400_000,
    };

    expect(isKvMetadataWithinLimit(JSON.stringify(oversizedServer))).toBe(false);
  });
});
