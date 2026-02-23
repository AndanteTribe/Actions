#!/bin/sh
set -e
for f in "$@"; do
    echo "Running $f"
    dotnet run -c Release "$f"
done
