FROM mcr.microsoft.com/dotnet/runtime:5.0-buster-slim-arm64v8 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS publish
COPY . .
WORKDIR "/src/NooliteMqttAdapter"
RUN dotnet publish "NooliteMqttAdapter.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "NooliteMqttAdapter.dll"]