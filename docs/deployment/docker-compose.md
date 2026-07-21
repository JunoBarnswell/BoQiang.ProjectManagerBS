# Docker Compose 部署

本配置部署一个纯静态 `web` 服务：保留镜像内的前端静态资源，只覆盖 Nginx 配置，不启动后端容器，也不代理后端接口。

后端必须独立部署，并通过 `FRONTEND_API_BASE_URL` 提供给浏览器访问。

默认从国内镜像和包源构建：

- Docker 基础镜像：`docker.m.daocloud.io`；
- npm：`https://registry.npmmirror.com`；
- NuGet：`https://repo.huaweicloud.com/repository/nuget/v3/index.json`。

所有地址都可以在 `.env` 中替换为企业 Harbor、ACR 或内部 npm/NuGet 源。

## 启动

在仓库根目录执行：

```powershell
Copy-Item .env.example .env
docker compose config
docker compose up -d --build
docker compose ps
```

默认访问地址：`http://localhost:8080`。

后端健康检查需要直接访问 `FRONTEND_API_BASE_URL` 对应的后端地址。

## 数据库和持久化目录

数据库、上传目录和 DataProtection keys 不由这个纯静态 Compose 管理，应由独立后端部署配置管理。

## 镜像和依赖源

执行完整无缓存构建：

```powershell
docker compose build --no-cache
```

如需切换镜像仓库，修改 `.env` 中的以下变量：

```env
NODE_IMAGE=docker.m.daocloud.io/library/node:22-bookworm-slim
DOTNET_SDK_IMAGE=docker.m.daocloud.io/mcr.microsoft.com/dotnet/sdk:10.0
DOTNET_ASPNET_IMAGE=docker.m.daocloud.io/mcr.microsoft.com/dotnet/aspnet:10.0
NGINX_IMAGE=docker.m.daocloud.io/library/nginx:1.29-alpine
NPM_REGISTRY=https://registry.npmmirror.com
NUGET_SOURCE=https://repo.huaweicloud.com/repository/nuget/v3/index.json
```

Dockerfile 使用独立的 `ARG` 接收这些值，因此不会把仓库地址固定在代码中。

Nginx 仅处理静态资源和 SPA 路由回退，不处理 `/api`、`/hubs`、`/uploads` 反向代理。

## 日志与排查

```powershell
docker compose logs -f web
docker compose exec web nginx -t
```

修改 `.env` 后重新构建并启动：

```powershell
docker compose up -d --build
```
