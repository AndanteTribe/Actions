FROM mcr.microsoft.com/dotnet/sdk:9.0@sha256:1330d3c4144aec2695036a125d9a3b49a1fc555c9024c981248fa83d40fa8b23
WORKDIR /work

ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global NuGetForUnity.Cli

ENTRYPOINT ["nugetforunity", "restore"]
