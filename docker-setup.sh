#!/usr/bin/env bash
set -euo pipefail

cd -- "$(dirname -- "${BASH_SOURCE[0]}")"
if [[ ! -f .env ]]; then
    echo "Copy .env.example to .env and configure local development values first." >&2
    exit 1
fi

# Uses Docker only; preserves the existing solution, source files, and SQL scripts.
docker compose up -d --build
docker compose ps

# Stop without deleting database data: docker compose down
# Optional full development database reset: docker compose down -v
# WARNING: -v deletes all database data. Never run it as part of normal startup.