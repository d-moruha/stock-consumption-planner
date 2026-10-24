FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["StockConsumptionPlanner.slnx", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["src/StockConsumptionPlanner.App/StockConsumptionPlanner.App.csproj", "src/StockConsumptionPlanner.App/"]
COPY ["tests/StockConsumptionPlanner.Tests/StockConsumptionPlanner.Tests.csproj", "tests/StockConsumptionPlanner.Tests/"]

RUN dotnet restore "src/StockConsumptionPlanner.App/StockConsumptionPlanner.App.csproj"

COPY ["src/", "src/"]
RUN dotnet publish "src/StockConsumptionPlanner.App/StockConsumptionPlanner.App.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app/data

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

USER $APP_UID
COPY --from=build /app/publish .

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -fsS http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "StockConsumptionPlanner.App.dll"]
