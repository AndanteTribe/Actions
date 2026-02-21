FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app

COPY SetOutPuts.cs ./SetOutPuts.cs
ENTRYPOINT ["dotnet", "run", "-c", "Release", "SetOutPuts.cs"]
