# =========================
# 1. BUILD STAGE
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything
COPY . .

# Restore
RUN dotnet restore "./presensi-kpu-batu-be.csproj"

# Publish
RUN dotnet publish "./presensi-kpu-batu-be.csproj" -c Release -o /app/publish

# =========================
# 2. RUNTIME STAGE
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Render requires the app to bind to PORT=10000
ENV ASPNETCORE_URLS=http://+:10000

EXPOSE 10000

ENTRYPOINT ["dotnet", "presensi-kpu-batu-be.dll"]
