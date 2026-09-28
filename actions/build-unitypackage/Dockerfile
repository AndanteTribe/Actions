FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:35d40304542c8689331f8cab17c65926cdf48fe711e289321d71924b230a7d29
WORKDIR /app

COPY . .
ENTRYPOINT ["dotnet", "run", "-c", "Release"]
