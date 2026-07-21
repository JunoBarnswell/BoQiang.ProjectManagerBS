# syntax=docker/dockerfile:1

FROM node:22-bookworm-slim AS frontend-build

WORKDIR /src/frontend/AsterERP.Web

COPY frontend/AsterERP.Web/package.json frontend/AsterERP.Web/package-lock.json ./
RUN npm ci

COPY frontend/AsterERP.Web/ ./
ENV VITE_APP_API_BASE_URL=/api
ENV VITE_APP_BASE_PATH=/
ENV VITE_APP_TARGET_APP_CODE=
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build

WORKDIR /src
COPY . .
RUN dotnet publish backend/AsterERP.Api/AsterERP.Api.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained true \
    --output /app/publish \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=None \
    -p:DebugSymbols=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS backend

WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=backend-build /app/publish/ ./
RUN mkdir -p /app/data

ENTRYPOINT ["/app/AsterERP.Api"]

FROM nginx:1.29-alpine AS frontend

COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=frontend-build /src/frontend/AsterERP.Web/dist/ /usr/share/nginx/html/

EXPOSE 80
