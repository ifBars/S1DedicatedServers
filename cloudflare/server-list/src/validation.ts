import { API_VERSION, type ActiveServer, type HeartbeatRequest } from "./contracts";

export type HeartbeatValidationResult =
  | { success: true; server: Omit<ActiveServer, "listingId" | "host" | "lastHeartbeat"> }
  | { success: false; message: string };

export function validateHeartbeat(value: unknown): HeartbeatValidationResult {
  if (!isRecord(value)) {
    return invalid("Heartbeat body must be a JSON object.");
  }

  const heartbeat = value as Partial<HeartbeatRequest>;
  if (heartbeat.protocolVersion !== API_VERSION) {
    return invalid(`Unsupported protocol version. Expected ${API_VERSION}.`);
  }

  const serverName = normalizeText(heartbeat.serverName, 100);
  const serverDescription = normalizeText(heartbeat.serverDescription, 280);
  const gameVersion = normalizeText(heartbeat.gameVersion, 50);
  const modVersion = normalizeText(heartbeat.modVersion, 50);
  if (!serverName || !gameVersion || !modVersion) {
    return invalid("Server name, game version, and mod version are required.");
  }

  if (!isIntegerInRange(heartbeat.currentPlayers, 0, 999)) {
    return invalid("Current player count must be between 0 and 999.");
  }

  if (!isIntegerInRange(heartbeat.maxPlayers, 1, 999)) {
    return invalid("Maximum player count must be between 1 and 999.");
  }

  if (heartbeat.currentPlayers > heartbeat.maxPlayers) {
    return invalid("Current player count cannot exceed maximum player count.");
  }

  if (!isIntegerInRange(heartbeat.port, 1, 65535)) {
    return invalid("Port must be between 1 and 65535.");
  }

  if (typeof heartbeat.passwordProtected !== "boolean") {
    return invalid("Password protection state is required.");
  }

  return {
    success: true,
    server: {
      protocolVersion: API_VERSION,
      serverName,
      serverDescription,
      currentPlayers: heartbeat.currentPlayers,
      maxPlayers: heartbeat.maxPlayers,
      port: heartbeat.port,
      passwordProtected: heartbeat.passwordProtected,
      gameVersion,
      modVersion,
    },
  };
}

function normalizeText(value: unknown, maxLength: number): string {
  return typeof value === "string" ? value.trim().slice(0, maxLength) : "";
}

function isIntegerInRange(value: unknown, minimum: number, maximum: number): value is number {
  return typeof value === "number" && Number.isInteger(value) && value >= minimum && value <= maximum;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function invalid(message: string): HeartbeatValidationResult {
  return { success: false, message };
}
