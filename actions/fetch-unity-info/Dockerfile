FROM mcr.microsoft.com/dotnet/sdk:11.0@sha256:65f3c7c6a25a239ab3e6719f2896147e7b68242ff2d8818dd70498bde6ef8bea
WORKDIR /app

# Keep dotnet off PID 1: .NET 11 can hang while reaping child processes.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tini \
    && rm -rf /var/lib/apt/lists/*

COPY . .
ENTRYPOINT ["/usr/bin/tini", "--", "dotnet", "run", "-c", "Release"]
