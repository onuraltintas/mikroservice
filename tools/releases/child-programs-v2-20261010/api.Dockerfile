FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY api/ ./
LABEL org.opencontainers.image.revision="77618cb9"
ENTRYPOINT ["dotnet", "SpeedReading.API.dll"]
