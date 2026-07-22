# 双镜像 Docker 部署

根目录 `Dockerfile` 从同一份 Git 源码构建两个运行镜像：

- `backend` target：.NET 10 `linux-x64` self-contained single-file API，监听容器内 `8080`；
- `frontend` target：Nginx、前端 `dist` 和反向代理配置，监听容器内 `80`。

不需要在前端和后端项目目录中额外创建 Dockerfile 或 Compose 文件。

运行时使用根目录 `compose.yaml`。它只拉取已推送到 ACR 的两个镜像：

```text
浏览器:8080 -> frontend/nginx:80
frontend/nginx -> backend:8080
```

Nginx 将 `/api`、`/hubs`、`/uploads` 代理到 Compose 网络中的 `backend:8080`。后端不映射宿主机端口。

## 部署

```bash
cp .env.deploy.example .env.deploy
vi .env.deploy

docker login registry.cn-hangzhou.aliyuncs.com
docker compose --env-file .env.deploy config
docker compose --env-file .env.deploy down --remove-orphans
docker compose --env-file .env.deploy pull
docker compose --env-file .env.deploy up -d --force-recreate
docker compose --env-file .env.deploy ps
```

访问地址：`http://服务器地址:8080`。

完整的 ACR 镜像构建、推送和变量配置见 [acr-compose.md](acr-compose.md)。
