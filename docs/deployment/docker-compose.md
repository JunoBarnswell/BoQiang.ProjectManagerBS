# Docker Compose 部署

本配置部署 `backend` 和 `frontend` 两个服务。浏览器只访问 Nginx，后端通过 Compose 内网服务名 `backend:8080` 提供 API。

前端构建使用 `/api`，Nginx 将 `/api`、`/hubs` 和 `/uploads` 转发到后端。

后端容器默认只暴露 Compose 内网端口 `8080`，不映射到宿主机；如需诊断，可临时使用 Compose override 映射宿主机端口。

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

固定访问地址：`http://localhost:8080`，Compose 映射为 `8080:80`。

健康检查地址：`http://localhost:8080/api/health`。

## 数据库和持久化目录

数据库、上传目录、Hangfire、DataProtection keys 和分布式锁统一持久化到 `ASTERERP_DATA_DIR`。

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

Nginx 处理静态资源和 SPA 路由回退，并代理 `/api`、`/hubs`、`/uploads` 到 `backend:8080`。

## 日志与排查

```powershell
docker compose logs -f backend
docker compose logs -f frontend
docker compose exec frontend nginx -t
```

修改 `.env` 后重新构建并启动：

```powershell
docker compose up -d --build
```
