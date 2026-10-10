FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG SOURCE_REVISION
WORKDIR /app
COPY api/ ./
LABEL org.opencontainers.image.revision=$SOURCE_REVISION
ENTRYPOINT ["dotnet", "SpeedReading.API.dll"]
