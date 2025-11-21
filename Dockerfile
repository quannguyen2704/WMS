# =======================
# 1) BUILD STAGE
# =======================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything
COPY . .

# Restore packages
RUN dotnet restore "./WMS.csproj"

# Build Release
RUN dotnet publish "./WMS.csproj" -c Release -o /app/publish

# =======================
# 2) RUN STAGE
# =======================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Expose port 8080 (Fly.io yêu cầu)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "WMS.dll"]

