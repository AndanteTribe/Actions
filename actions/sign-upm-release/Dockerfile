FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b
WORKDIR /app

# The UPM CLI archive targets Linux x64. Reject other architectures at image build time.
RUN test "$(uname -m)" = x86_64 \
    && apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl unzip \
    && rm -rf /var/lib/apt/lists/*

ARG UPM_CLI_VERSION=v9.36.0
ARG UPM_CLI_SHA256=a7c00db477daa4161f24bf0af5d2589e88f3e6276f9798d44bae601d957dbe04
RUN curl -fsSL --retry 3 "https://cdn.packages.unity.com/upm-cli/releases/${UPM_CLI_VERSION}/upm-linux-x64.zip" -o /tmp/upm.zip \
    && echo "${UPM_CLI_SHA256}  /tmp/upm.zip" | sha256sum --check --status \
    && unzip -q /tmp/upm.zip -d /opt/upm \
    && chmod +x /opt/upm/UnityPackageManager \
    && ln -s /opt/upm/UnityPackageManager /usr/local/bin/upm \
    && rm /tmp/upm.zip \
    && upm --help

COPY . .
ENTRYPOINT ["dotnet", "run", "-c", "Release"]
