FROM mcr.microsoft.com/dotnet/sdk:9.0@sha256:7e3a4bd66701a451823db2028154ae16ff191bc452596ba618ef4d0c58f9cd01
WORKDIR /work

ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global NuGetForUnity.Cli

ENTRYPOINT ["nugetforunity", "restore"]
