#!/bin/bash
set -e
echo "DB_HOST=$DB_HOST"
echo "DB_PORT=$DB_PORT"
echo "DB_ADMIN_USER=$DB_ADMIN_USER"
echo "APP_DB_NAME=$APP_DB_NAME"
echo "APP_DB_USER=$APP_DB_USER"
echo "DUMP_FILE=$DUMP_FILE"
echo "Waiting for PostgreSQL to accept connections..."

until pg_isready -h "$DB_HOST" -p "$DB_PORT" -U "$DB_ADMIN_USER" -d postgres; do
  sleep 2
done

echo "PostgreSQL is ready."

echo "Ensuring app role exists..."
psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_ADMIN_USER" -d postgres -v ON_ERROR_STOP=1 <<EOF
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

echo "Terminating existing connections to ${APP_DB_NAME}..."
psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_ADMIN_USER" -d postgres -v ON_ERROR_STOP=1 -c "
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = '${APP_DB_NAME}'
  AND pid <> pg_backend_pid();
"

echo "Dropping database if it exists..."
psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_ADMIN_USER" -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS ${APP_DB_NAME};"

echo "Creating database..."
psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_ADMIN_USER" -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE ${APP_DB_NAME} OWNER ${APP_DB_USER};"

echo "Restoring dump from ${DUMP_FILE}..."

if [[ "$DUMP_FILE" == *.sql ]]; then
  psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_ADMIN_USER" -d "$APP_DB_NAME" -v ON_ERROR_STOP=1 -f "$DUMP_FILE"
elif [[ "$DUMP_FILE" == *.dump ]]; then
  pg_restore -h "$DB_HOST" -p "$DB_PORT" -U "$DB_ADMIN_USER" -d "$APP_DB_NAME" --no-owner --role="$APP_DB_USER" "$DUMP_FILE"
else
  echo "Unsupported dump format: $DUMP_FILE"
  exit 1
fi

echo "Granting ownership..."
psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_ADMIN_USER" -d postgres -v ON_ERROR_STOP=1 -c "ALTER DATABASE ${APP_DB_NAME} OWNER TO ${APP_DB_USER};"

echo "Restore completed successfully."