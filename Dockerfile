ARG NODE_IMAGE=docker.m.daocloud.io/library/node:22-bookworm-slim
ARG DOTNET_SDK_IMAGE=docker.m.daocloud.io/mcr.microsoft.com/dotnet/sdk:10.0
ARG DOTNET_ASPNET_IMAGE=docker.m.daocloud.io/mcr.microsoft.com/dotnet/aspnet:10.0
ARG NGINX_IMAGE=docker.m.daocloud.io/library/nginx:1.29-alpine

FROM ${NODE_IMAGE} AS frontend-build

WORKDIR /src/frontend/AsterERP.Web

ARG FRONTEND_API_BASE_URL=https://api.astererp.example/api
ARG NPM_REGISTRY=https://registry.npmmirror.com

COPY frontend/AsterERP.Web/package.json frontend/AsterERP.Web/package-lock.json ./
RUN npm config set registry "${NPM_REGISTRY}" && npm ci

COPY frontend/AsterERP.Web/ ./
ENV VITE_APP_BASE_PATH=/
ENV VITE_APP_TARGET_APP_CODE=
ENV VITE_APP_API_BASE_URL=${FRONTEND_API_BASE_URL}
RUN npm run build

FROM ${DOTNET_SDK_IMAGE} AS backend-build

WORKDIR /src
ARG NUGET_SOURCE=https://repo.huaweicloud.com/repository/nuget/v3/index.json
COPY . .
RUN dotnet restore backend/AsterERP.Api/AsterERP.Api.csproj \
    --runtime linux-x64 \
    --source "${NUGET_SOURCE}"
RUN dotnet publish backend/AsterERP.Api/AsterERP.Api.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained true \
    --output /app/publish \
    --no-restore \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=None \
    -p:DebugSymbols=false

FROM ${DOTNET_ASPNET_IMAGE} AS backend

WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=backend-build /app/publish/ ./
RUN mkdir -p /app/data

ENTRYPOINT ["/app/AsterERP.Api"]

FROM ${NGINX_IMAGE} AS frontend

COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=frontend-build /src/frontend/AsterERP.Web/dist/ /usr/share/nginx/html/

EXPOSE 80
