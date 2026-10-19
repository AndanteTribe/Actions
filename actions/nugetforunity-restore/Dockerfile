FROM mcr.microsoft.com/dotnet/sdk:9.0@sha256:2f5eb32870b4c0074a3d5b5191a03553bcab7b74463a76a7c0a5f3ef65c90fe5
WORKDIR /work

ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global NuGetForUnity.Cli

ENTRYPOINT ["nugetforunity", "restore"]
