FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY api/ ./
LABEL org.opencontainers.image.revision="6f315ff3"
ENTRYPOINT ["dotnet", "SpeedReading.API.dll"]
