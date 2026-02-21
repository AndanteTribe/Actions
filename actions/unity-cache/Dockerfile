# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /build
COPY src/ .
RUN dotnet publish -c Release -o /app UnityCache.csproj

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM debian:bookworm-slim
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates \
      tar \
      zstd \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .
COPY entrypoint.sh /entrypoint.sh
COPY post.sh /post.sh
RUN chmod +x /entrypoint.sh /post.sh

ENTRYPOINT ["/entrypoint.sh"]
