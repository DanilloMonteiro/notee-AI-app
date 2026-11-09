# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/NoteLab/NoteLab.csproj src/NoteLab/
RUN dotnet restore src/NoteLab/NoteLab.csproj

COPY src/ src/
RUN dotnet publish src/NoteLab/NoteLab.csproj -c Release -o /app/publish --no-restore

# Bundle executável das migrations do EF Core (usado pelo serviço "migrate")
RUN dotnet tool install --global dotnet-ef --version 10.0.12
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet ef migrations bundle --project src/NoteLab/NoteLab.csproj \
    --self-contained -r linux-x64 -o /app/efbundle --force

# ---- Migrations ----
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrate
WORKDIR /app
COPY --from=build /app/efbundle .
ENTRYPOINT ["./efbundle"]

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=5000
EXPOSE 5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NoteLab.dll"]
