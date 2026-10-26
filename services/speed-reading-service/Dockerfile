FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["services/speed-reading-service/SpeedReading.API/SpeedReading.API.csproj", "services/speed-reading-service/SpeedReading.API/"]
COPY ["services/speed-reading-service/SpeedReading.Application/SpeedReading.Application.csproj", "services/speed-reading-service/SpeedReading.Application/"]
COPY ["services/speed-reading-service/SpeedReading.Domain/SpeedReading.Domain.csproj", "services/speed-reading-service/SpeedReading.Domain/"]
COPY ["services/speed-reading-service/SpeedReading.Infrastructure/SpeedReading.Infrastructure.csproj", "services/speed-reading-service/SpeedReading.Infrastructure/"]
COPY ["shared/EduPlatform.Shared.Kernel/EduPlatform.Shared.Kernel.csproj", "shared/EduPlatform.Shared.Kernel/"]
COPY ["shared/EduPlatform.Shared.Contracts/EduPlatform.Shared.Contracts.csproj", "shared/EduPlatform.Shared.Contracts/"]
COPY ["shared/EduPlatform.Shared.Infrastructure/EduPlatform.Shared.Infrastructure.csproj", "shared/EduPlatform.Shared.Infrastructure/"]
COPY ["shared/EduPlatform.Shared.Security/EduPlatform.Shared.Security.csproj", "shared/EduPlatform.Shared.Security/"]

RUN dotnet restore "services/speed-reading-service/SpeedReading.API/SpeedReading.API.csproj"
COPY . .
WORKDIR "/src/services/speed-reading-service/SpeedReading.API"
RUN dotnet build "SpeedReading.API.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "SpeedReading.API.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# Data Protection keys and CMS media live on named volumes mounted here. Creating the
# mount points owned by the runtime user makes fresh volumes writable; volumes created
# by older root images are fixed by speed-reading-volume-permissions.
RUN mkdir -p /var/lib/eduplatform/data-protection-keys /var/lib/eduplatform/media \
    && chown -R "$APP_UID" /var/lib/eduplatform
COPY --from=publish /app/publish .
# Run as the image's built-in non-root user (UID 1654); the service listens on 8080.
USER $APP_UID
ENTRYPOINT ["dotnet", "SpeedReading.API.dll"]
