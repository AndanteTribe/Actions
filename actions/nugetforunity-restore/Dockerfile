FROM mcr.microsoft.com/dotnet/sdk:9.0@sha256:20387c6674c30e46def0cc8cb557bd2a69b35afdf18c19c3694638d0a90897e4
WORKDIR /work

ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global NuGetForUnity.Cli

ENTRYPOINT ["nugetforunity", "restore"]
