FROM mcr.microsoft.com/dotnet/sdk:10.0

# docker CLI is required to build the Unity image and start the container via the host Docker socket
RUN apt-get update && apt-get install -y --no-install-recommends docker.io && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY . .

ENTRYPOINT ["dotnet", "run", "-c", "Release"]

