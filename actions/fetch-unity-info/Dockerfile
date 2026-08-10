FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0
WORKDIR /app

COPY . .
ENTRYPOINT ["dotnet", "run", "-c", "Release"]
