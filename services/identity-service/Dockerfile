FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["services/identity-service/Identity.API/Identity.API.csproj", "services/identity-service/Identity.API/"]
COPY ["services/identity-service/Identity.Application/Identity.Application.csproj", "services/identity-service/Identity.Application/"]
COPY ["services/identity-service/Identity.Domain/Identity.Domain.csproj", "services/identity-service/Identity.Domain/"]
COPY ["services/identity-service/Identity.Infrastructure/Identity.Infrastructure.csproj", "services/identity-service/Identity.Infrastructure/"]
COPY ["shared/EduPlatform.Shared.Kernel/EduPlatform.Shared.Kernel.csproj", "shared/EduPlatform.Shared.Kernel/"]
COPY ["shared/EduPlatform.Shared.Infrastructure/EduPlatform.Shared.Infrastructure.csproj", "shared/EduPlatform.Shared.Infrastructure/"]
COPY ["shared/EduPlatform.Shared.Security/EduPlatform.Shared.Security.csproj", "shared/EduPlatform.Shared.Security/"]
COPY ["shared/EduPlatform.Shared.Contracts/EduPlatform.Shared.Contracts.csproj", "shared/EduPlatform.Shared.Contracts/"]

# Package versions are managed centrally for the whole repository.
COPY ["Directory.Packages.props", "./"]
RUN dotnet restore "services/identity-service/Identity.API/Identity.API.csproj"

# Copy everything else and publish (publish builds; a separate build step only repeated it)
COPY . .
WORKDIR "/src/services/identity-service/Identity.API"

FROM build AS publish
RUN dotnet publish "Identity.API.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# The Data Protection key ring lives on a named volume mounted here. Creating the mount
# point owned by the runtime user makes a fresh volume inherit writable ownership;
# volumes created by older root images are fixed by identity-volume-permissions.
RUN mkdir -p /var/lib/eduplatform/data-protection-keys \
    && chown -R "$APP_UID" /var/lib/eduplatform
COPY --from=publish /app/publish .
# Run as the image's built-in non-root user (UID 1654); the service listens on 8080.
USER $APP_UID
ENTRYPOINT ["dotnet", "Identity.API.dll"]
