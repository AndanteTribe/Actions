FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app

COPY . .
RUN chmod +x /app/entrypoint.sh
ENTRYPOINT ["/app/entrypoint.sh"]
