#!/bin/bash
set -e

echo "Waiting for PostgreSQL..."

until pg_isready -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_ADMIN_USER}" -d postgres; do
  sleep 2
done

echo "PostgreSQL is ready."

echo "Ensuring app role exists..."
psql -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_ADMIN_USER}" -d postgres -v ON_ERROR_STOP=1 <<EOF
DO \$\$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_roles WHERE rolname = '${APP_DB_USER}'
    ) THEN
        EXECUTE format('CREATE ROLE %I WITH LOGIN PASSWORD %L', '${APP_DB_USER}', '${APP_DB_PASSWORD}');
    ELSE
        EXECUTE format('ALTER ROLE %I WITH LOGIN PASSWORD %L', '${APP_DB_USER}', '${APP_DB_PASSWORD}');
    END IF;
END
\$\$;
EOF

# Create the database only when it doesn't exist yet. Dropping it here would wipe all data
# (users, contests, trainings, synced solves) on EVERY `docker compose up` — to reset the DB
# on purpose, run `docker compose down -v` (drops the postgres volume) and start again.
echo "Ensuring database exists..."
DB_EXISTS=$(psql -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_ADMIN_USER}" -d postgres -tAc \
  "SELECT 1 FROM pg_database WHERE datname = '${APP_DB_NAME}';")

if [ "${DB_EXISTS}" != "1" ]; then
  echo "Creating database..."
  psql -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_ADMIN_USER}" -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE ${APP_DB_NAME} OWNER ${APP_DB_USER};"
else
  echo "Database already exists; leaving data intact."
fi

echo "Database check complete."