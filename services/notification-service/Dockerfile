FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["services/notification-service/Notification.API/Notification.API.csproj", "services/notification-service/Notification.API/"]
COPY ["services/notification-service/Notification.Application/Notification.Application.csproj", "services/notification-service/Notification.Application/"]
COPY ["services/notification-service/Notification.Domain/Notification.Domain.csproj", "services/notification-service/Notification.Domain/"]
COPY ["services/notification-service/Notification.Infrastructure/Notification.Infrastructure.csproj", "services/notification-service/Notification.Infrastructure/"]
COPY ["shared/EduPlatform.Shared.Kernel/EduPlatform.Shared.Kernel.csproj", "shared/EduPlatform.Shared.Kernel/"]
COPY ["shared/EduPlatform.Shared.Contracts/EduPlatform.Shared.Contracts.csproj", "shared/EduPlatform.Shared.Contracts/"]
COPY ["shared/EduPlatform.Shared.Infrastructure/EduPlatform.Shared.Infrastructure.csproj", "shared/EduPlatform.Shared.Infrastructure/"]
COPY ["shared/EduPlatform.Shared.Security/EduPlatform.Shared.Security.csproj", "shared/EduPlatform.Shared.Security/"]

# Package versions are managed centrally for the whole repository.
COPY ["Directory.Packages.props", "./"]
RUN dotnet restore "services/notification-service/Notification.API/Notification.API.csproj"

# Copy everything else and publish (publish builds; a separate build step only repeated it)
COPY . .
WORKDIR "/src/services/notification-service/Notification.API"

FROM build AS publish
RUN dotnet publish "Notification.API.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# The Data Protection key ring (it also decrypts queued e-mail bodies) lives on a named
# volume mounted here. A fresh volume inherits the runtime user's ownership; volumes
# created by older root images are fixed by notification-volume-permissions.
RUN mkdir -p /var/lib/eduplatform/data-protection-keys \
    && chown -R "$APP_UID" /var/lib/eduplatform
COPY --from=publish /app/publish .
# Run as the image's built-in non-root user (UID 1654); the service listens on 8080.
USER $APP_UID
ENTRYPOINT ["dotnet", "Notification.API.dll"]
