FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:2fa828c68761b1b8c23d7662dc134421b9d3b59fe1425fdbc80804e390cdb24d
WORKDIR /app

COPY . .
ENTRYPOINT ["dotnet", "run", "-c", "Release"]
