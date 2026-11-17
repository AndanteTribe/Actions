FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e
WORKDIR /app

# The UPM CLI archive targets Linux x64. Reject other architectures at image build time.
RUN test "$(uname -m)" = x86_64

ARG UPM_CLI_VERSION=v9.36.0
ARG UPM_CLI_SHA256=a7c00db477daa4161f24bf0af5d2589e88f3e6276f9798d44bae601d957dbe04
RUN curl -fsSL --retry 3 "https://cdn.packages.unity.com/upm-cli/releases/${UPM_CLI_VERSION}/upm-linux-x64.zip" -o /tmp/upm.zip \
    && echo "${UPM_CLI_SHA256}  /tmp/upm.zip" | sha256sum --check --status \
    && echo 'System.IO.Compression.ZipFile.ExtractToDirectory(args[0], args[1]);' \
        | dotnet run - -c Release -p:PublishAot=false -- /tmp/upm.zip /opt/upm \
    && chmod +x /opt/upm/UnityPackageManager \
    && ln -s /opt/upm/UnityPackageManager /usr/local/bin/upm \
    && rm /tmp/upm.zip \
    && upm --help

COPY . .
ENTRYPOINT ["dotnet", "run", "-c", "Release"]
