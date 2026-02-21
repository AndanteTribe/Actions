FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app

COPY cache/SetOutputs.cs ./cache/SetOutputs.cs
COPY share/FetchUnityProjectInfo.cs ./share/FetchUnityProjectInfo.cs
ENTRYPOINT ["dotnet", "run", "-c", "Release"]
