# 阿里云 ACR 部署版 Compose

根目录 `compose.yaml` 是运行时部署配置，只拉取 ACR 中已经构建好的后端和前端镜像，不在服务器现场编译源码。

## 镜像约定

需要向 ACR 推送两个镜像：

```text
<ACR_REGISTRY>/<ACR_NAMESPACE>/boqiang-projectmanagerbs-backend:<IMAGE_TAG>
<ACR_REGISTRY>/<ACR_NAMESPACE>/boqiang-projectmanagerbs-frontend:<IMAGE_TAG>
```

前端镜像必须使用 `FRONTEND_API_BASE_URL=/api` 构建，并且必须包含 `deploy/nginx.conf`。Nginx 会把 `/api`、`/hubs`、`/uploads` 转发到同一 Compose 网络中的 `backend:8080`。

## 构建并推送镜像

在项目根目录执行：

```bash
export ACR_REGISTRY=registry.cn-hangzhou.aliyuncs.com
export ACR_NAMESPACE=your-acr-namespace
export IMAGE_TAG=latest

docker login "$ACR_REGISTRY"

docker build --target backend \
  -t "$ACR_REGISTRY/$ACR_NAMESPACE/boqiang-projectmanagerbs-backend:$IMAGE_TAG" .

docker build --target frontend \
  --build-arg FRONTEND_API_BASE_URL=/api \
  -t "$ACR_REGISTRY/$ACR_NAMESPACE/boqiang-projectmanagerbs-frontend:$IMAGE_TAG" .

docker push "$ACR_REGISTRY/$ACR_NAMESPACE/boqiang-projectmanagerbs-backend:$IMAGE_TAG"
docker push "$ACR_REGISTRY/$ACR_NAMESPACE/boqiang-projectmanagerbs-frontend:$IMAGE_TAG"
```

如构建机需要国内源，Dockerfile 默认使用国内 Docker、npm 和 NuGet 源，也可以通过 `--build-arg` 覆盖。

## 服务器部署

将 `.env.deploy.example` 复制为 `.env.deploy`，填写 ACR 命名空间和数据目录：

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

访问地址固定为：`http://服务器地址:8080`。

## 运行结构

- `backend`：内网 `8080`，不映射宿主机端口；
- `frontend`：宿主机 `8080` 映射到容器 `80`；
- 两个服务固定加入 `boqiang-projectmanagerbs-production-network`；
- 数据目录通过 `ASTERERP_DATA_DIR` 持久化到后端容器 `/app/data`。
