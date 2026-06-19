FROM mcr.microsoft.com/dotnet/sdk:9.0@sha256:1f2cb07e7ced57c4ea61163d485a2ee1cffd63763c6b17a4447ce734ff236475
WORKDIR /work

ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global NuGetForUnity.Cli

ENTRYPOINT ["nugetforunity", "restore"]
