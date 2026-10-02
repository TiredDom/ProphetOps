# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0
ARG NODE_VERSION=22-alpine

FROM node:${NODE_VERSION} AS client-build
WORKDIR /src/client
COPY client/package*.json ./
RUN npm ci
COPY client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS publish
WORKDIR /src
COPY ProphetOps.Domain/*.csproj ProphetOps.Domain/
COPY ProphetOps.Forecasting/*.csproj ProphetOps.Forecasting/
COPY ProphetOps.Data/*.csproj ProphetOps.Data/
COPY ProphetOps.PostgresMigrations/*.csproj ProphetOps.PostgresMigrations/
COPY ProphetOps.Api/*.csproj ProphetOps.Api/
RUN dotnet restore ProphetOps.Api/ProphetOps.Api.csproj

COPY ProphetOps.Domain/ ProphetOps.Domain/
COPY ProphetOps.Forecasting/ ProphetOps.Forecasting/
COPY ProphetOps.Data/ ProphetOps.Data/
COPY ProphetOps.PostgresMigrations/ ProphetOps.PostgresMigrations/
COPY ProphetOps.Api/ ProphetOps.Api/
COPY --from=client-build /src/ProphetOps.Api/wwwroot ProphetOps.Api/wwwroot
RUN dotnet publish ProphetOps.Api/ProphetOps.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
COPY --from=publish /app/publish ./
RUN mkdir -p /app/storage /tmp/prophetops \
    && chown -R $APP_UID /app/storage /tmp/prophetops
USER $APP_UID
ENTRYPOINT ["dotnet", "ProphetOps.Api.dll"]
