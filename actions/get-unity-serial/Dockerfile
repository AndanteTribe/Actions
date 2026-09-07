FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510
WORKDIR /app

COPY . .
ENTRYPOINT ["dotnet", "run", "-c", "Release"]
