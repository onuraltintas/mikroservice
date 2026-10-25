FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["services/api-gateway/EduPlatform.Gateway.csproj", "services/api-gateway/"]
COPY ["shared/EduPlatform.Shared.Kernel/EduPlatform.Shared.Kernel.csproj", "shared/EduPlatform.Shared.Kernel/"]
COPY ["shared/EduPlatform.Shared.Contracts/EduPlatform.Shared.Contracts.csproj", "shared/EduPlatform.Shared.Contracts/"]
COPY ["shared/EduPlatform.Shared.Infrastructure/EduPlatform.Shared.Infrastructure.csproj", "shared/EduPlatform.Shared.Infrastructure/"]
COPY ["shared/EduPlatform.Shared.Security/EduPlatform.Shared.Security.csproj", "shared/EduPlatform.Shared.Security/"]
RUN dotnet restore "services/api-gateway/EduPlatform.Gateway.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/services/api-gateway"
RUN dotnet build "EduPlatform.Gateway.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "EduPlatform.Gateway.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# The Data Protection key ring lives on a named volume mounted here. Creating the mount
# point owned by the runtime user makes a fresh volume inherit writable ownership;
# volumes created by older root images are fixed by api-gateway-volume-permissions.
RUN mkdir -p /var/lib/eduplatform/data-protection-keys \
    && chown -R "$APP_UID" /var/lib/eduplatform
COPY --from=publish /app/publish .
# Run as the image's built-in non-root user (UID 1654); the gateway listens on 8080.
USER $APP_UID
ENTRYPOINT ["dotnet", "EduPlatform.Gateway.dll"]
