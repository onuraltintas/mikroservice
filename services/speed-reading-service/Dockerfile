FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["services/speed-reading-service/SpeedReading.API/SpeedReading.API.csproj", "services/speed-reading-service/SpeedReading.API/"]
COPY ["services/speed-reading-service/SpeedReading.Application/SpeedReading.Application.csproj", "services/speed-reading-service/SpeedReading.Application/"]
COPY ["services/speed-reading-service/SpeedReading.Domain/SpeedReading.Domain.csproj", "services/speed-reading-service/SpeedReading.Domain/"]
COPY ["services/speed-reading-service/SpeedReading.Infrastructure/SpeedReading.Infrastructure.csproj", "services/speed-reading-service/SpeedReading.Infrastructure/"]
COPY ["shared/EduPlatform.Shared.Kernel/EduPlatform.Shared.Kernel.csproj", "shared/EduPlatform.Shared.Kernel/"]
COPY ["shared/EduPlatform.Shared.Infrastructure/EduPlatform.Shared.Infrastructure.csproj", "shared/EduPlatform.Shared.Infrastructure/"]
COPY ["shared/EduPlatform.Shared.Security/EduPlatform.Shared.Security.csproj", "shared/EduPlatform.Shared.Security/"]

RUN dotnet restore "services/speed-reading-service/SpeedReading.API/SpeedReading.API.csproj"
COPY . .
WORKDIR "/src/services/speed-reading-service/SpeedReading.API"
RUN dotnet build "SpeedReading.API.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "SpeedReading.API.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SpeedReading.API.dll"]
