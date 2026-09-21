CREATE TABLE IF NOT EXISTS portal_operators (
    id TEXT PRIMARY KEY,
    state TEXT NOT NULL DEFAULT 'active' CHECK (state IN ('active', 'suspended')),
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS portal_identities (
    provider TEXT NOT NULL CHECK (provider IN ('discord', 'steam')),
    subject TEXT NOT NULL,
    operator_id TEXT NOT NULL,
    display_name TEXT NOT NULL,
    created_at INTEGER NOT NULL,
    last_login_at INTEGER NOT NULL,
    PRIMARY KEY (provider, subject),
    FOREIGN KEY (operator_id) REFERENCES portal_operators(id)
);

CREATE INDEX IF NOT EXISTS idx_portal_identities_operator
    ON portal_identities(operator_id);

CREATE TABLE IF NOT EXISTS portal_sessions (
    token_hash TEXT PRIMARY KEY,
    operator_id TEXT NOT NULL,
    csrf_token TEXT NOT NULL,
    created_at INTEGER NOT NULL,
    expires_at INTEGER NOT NULL,
    FOREIGN KEY (operator_id) REFERENCES portal_operators(id)
);

CREATE INDEX IF NOT EXISTS idx_portal_sessions_expiry
    ON portal_sessions(expires_at);

CREATE TABLE IF NOT EXISTS portal_oauth_states (
    state_hash TEXT PRIMARY KEY,
    provider TEXT NOT NULL CHECK (provider IN ('discord', 'steam')),
    created_at INTEGER NOT NULL,
    expires_at INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_portal_oauth_states_expiry
    ON portal_oauth_states(expires_at);

ALTER TABLE server_listings_v2 ADD COLUMN operator_id TEXT REFERENCES portal_operators(id);
ALTER TABLE server_listings_v2 ADD COLUMN label TEXT NOT NULL DEFAULT 'Dedicated server';

CREATE INDEX IF NOT EXISTS idx_server_listings_v2_operator
    ON server_listings_v2(operator_id, state);
