#!/usr/bin/env bash
set -euo pipefail

host="$1"
shift
port=1433
if [ "$1" != "" ]; then
  port="$1"
fi

echo "Waiting for $host:$port..."
until nc -z "$host" "$port"; do
  >&2 echo "Waiting for database..."
  sleep 2
done
echo "Database is available"
