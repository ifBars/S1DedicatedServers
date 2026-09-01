export const API_VERSION = 2;
export const ACTIVE_SERVER_PREFIX = "v2:active:";
export const ACTIVE_SERVER_TTL_SECONDS = 15 * 60;
export const MAX_REQUEST_BYTES = 16 * 1024;
export const MAX_KV_METADATA_BYTES = 1024;

export interface RegisterListingResponse {
  success: true;
  listingId: string;
  secret: string;
}

export interface HeartbeatRequest {
  protocolVersion: number;
  serverName: string;
  serverDescription?: string;
  currentPlayers: number;
  maxPlayers: number;
  port: number;
  passwordProtected: boolean;
  gameVersion: string;
  modVersion: string;
}

export interface ActiveServer {
  listingId: string;
  protocolVersion: number;
  serverName: string;
  serverDescription: string;
  currentPlayers: number;
  maxPlayers: number;
  port: number;
  host: string;
  passwordProtected: boolean;
  gameVersion: string;
  modVersion: string;
  lastHeartbeat: number;
}

export interface ServerListResponse {
  success: true;
  servers: ActiveServer[];
  nextCursor?: string;
}

export interface ErrorResponse {
  success: false;
  error: string;
  message: string;
}

export interface ListingRecord {
  state: "active" | "revoked" | "banned";
  last_persisted_at: number | null;
}

export interface ActiveListingIdRecord {
  id: string;
}
