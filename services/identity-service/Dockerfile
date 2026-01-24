FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
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

RUN dotnet restore "services/identity-service/Identity.API/Identity.API.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/services/identity-service/Identity.API"
RUN dotnet build "Identity.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Identity.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Identity.API.dll"]
