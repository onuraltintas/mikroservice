FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["services/coaching-service/Coaching.API/Coaching.API.csproj", "services/coaching-service/Coaching.API/"]
COPY ["services/coaching-service/Coaching.Application/Coaching.Application.csproj", "services/coaching-service/Coaching.Application/"]
COPY ["services/coaching-service/Coaching.Domain/Coaching.Domain.csproj", "services/coaching-service/Coaching.Domain/"]
COPY ["services/coaching-service/Coaching.Infrastructure/Coaching.Infrastructure.csproj", "services/coaching-service/Coaching.Infrastructure/"]
COPY ["shared/EduPlatform.Shared.Kernel/EduPlatform.Shared.Kernel.csproj", "shared/EduPlatform.Shared.Kernel/"]
COPY ["shared/EduPlatform.Shared.Infrastructure/EduPlatform.Shared.Infrastructure.csproj", "shared/EduPlatform.Shared.Infrastructure/"]
COPY ["shared/EduPlatform.Shared.Security/EduPlatform.Shared.Security.csproj", "shared/EduPlatform.Shared.Security/"]
COPY ["shared/EduPlatform.Shared.Contracts/EduPlatform.Shared.Contracts.csproj", "shared/EduPlatform.Shared.Contracts/"]

RUN dotnet restore "services/coaching-service/Coaching.API/Coaching.API.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/services/coaching-service/Coaching.API"
RUN dotnet build "Coaching.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Coaching.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Coaching.API.dll"]
