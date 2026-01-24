FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["services/api-gateway/EduPlatform.Gateway.csproj", "services/api-gateway/"]
RUN dotnet restore "services/api-gateway/EduPlatform.Gateway.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/services/api-gateway"
RUN dotnet build "EduPlatform.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EduPlatform.Gateway.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EduPlatform.Gateway.dll"]
