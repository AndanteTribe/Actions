FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:ed034a8bf0b24ded0cbbac07e17825d8e9ebfe21e308191d0f7421eaf5ad4664
WORKDIR /app

COPY . .
ENTRYPOINT ["dotnet", "run", "-c", "Release"]
