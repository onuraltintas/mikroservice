FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY coaching/ ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Coaching.API.dll"]
