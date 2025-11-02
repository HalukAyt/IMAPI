# 1. Build aşaması
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# .sln ve projeleri kopyala
COPY . .

# Restore
RUN dotnet restore ./IMAPI/IMAPI.csproj

# Publish
RUN dotnet publish ./IMAPI/IMAPI.csproj -c Release -o /app/publish

# 2. Runtime aşaması
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render için uygun port
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Doğru entrypoint
ENTRYPOINT ["dotnet", "IMAPI.dll"]
