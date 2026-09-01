CREATE TABLE IF NOT EXISTS server_listings_v2 (
    id TEXT PRIMARY KEY,
    secret_hash TEXT NOT NULL,
    state TEXT NOT NULL DEFAULT 'active' CHECK (state IN ('active', 'revoked', 'banned')),
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    last_seen INTEGER,
    last_persisted_at INTEGER,
    last_ip TEXT
);

CREATE INDEX IF NOT EXISTS idx_server_listings_v2_state
    ON server_listings_v2(state);

CREATE INDEX IF NOT EXISTS idx_server_listings_v2_last_seen
    ON server_listings_v2(last_seen);
