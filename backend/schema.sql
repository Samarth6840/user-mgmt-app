CREATE TABLE IF NOT EXISTS users (
  id UUID NOT NULL PRIMARY KEY,
  name VARCHAR(255) NOT NULL,
  email VARCHAR(320) NOT NULL,
  password_hash VARCHAR(255) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'unverified',
  verification_token UUID DEFAULT NULL,
  last_login TIMESTAMP DEFAULT NULL,
  last_activity TIMESTAMP DEFAULT NULL,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email_unique ON users (email);

CREATE INDEX IF NOT EXISTS idx_users_last_activity ON users (last_activity);