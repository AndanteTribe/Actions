FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app
COPY FetchUnityProjectInfo.cs .
ENTRYPOINT ["dotnet", "run", "-c", "Release", "FetchUnityProjectInfo.cs"]
