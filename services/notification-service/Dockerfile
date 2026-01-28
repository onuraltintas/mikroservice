FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["services/notification-service/Notification.API/Notification.API.csproj", "services/notification-service/Notification.API/"]
COPY ["services/notification-service/Notification.Application/Notification.Application.csproj", "services/notification-service/Notification.Application/"]
COPY ["services/notification-service/Notification.Domain/Notification.Domain.csproj", "services/notification-service/Notification.Domain/"]
COPY ["services/notification-service/Notification.Infrastructure/Notification.Infrastructure.csproj", "services/notification-service/Notification.Infrastructure/"]
COPY ["shared/EduPlatform.Shared.Kernel/EduPlatform.Shared.Kernel.csproj", "shared/EduPlatform.Shared.Kernel/"]
COPY ["shared/EduPlatform.Shared.Contracts/EduPlatform.Shared.Contracts.csproj", "shared/EduPlatform.Shared.Contracts/"]

RUN dotnet restore "services/notification-service/Notification.API/Notification.API.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/services/notification-service/Notification.API"
RUN dotnet build "Notification.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Notification.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Notification.API.dll"]
