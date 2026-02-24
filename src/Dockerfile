FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app

COPY . .
ENTRYPOINT ["/bin/sh", "-c", "set -e; for f in \"$@\"; do dotnet run -c Release \"$f\"; done", "sh"]
