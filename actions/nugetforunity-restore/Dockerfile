FROM mcr.microsoft.com/dotnet/sdk:9.0@sha256:d7cfd59385751fc87897a138ba173fb1bbc2bf946ea78aecc91a119993676997
WORKDIR /work

ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global NuGetForUnity.Cli

ENTRYPOINT ["nugetforunity", "restore"]
