# =============================
# 1. Build stage
# =============================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore "WMS.csproj"
RUN dotnet publish "WMS.csproj" -c Release -o /app/publish

# =============================
# 2. Runtime stage
# =============================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:$PORT

ENTRYPOINT ["dotnet", "WMS.dll"]
