# Docker Compose 部署

本项目使用根目录 `Dockerfile` 构建两个镜像目标：

- `backend`：.NET 10 Linux 单文件后端；
- `frontend`：Nginx 静态前端，并代理后端接口。

## 启动

在仓库根目录执行：

```powershell
Copy-Item .env.example .env
docker compose config
docker compose up -d --build
docker compose ps
```

默认访问地址：`http://localhost:8080`。

后端健康检查地址：`http://localhost:8080/api/health`。

## 数据库和持久化目录

`.env` 中的 `ASTERERP_DATA_DIR` 是宿主机目录，容器内固定挂载为 `/app/data`。

`ASTERERP_CONNECTION_STRING` 是后端容器内实际使用的 SQLite 连接串：

```env
ASTERERP_DATA_DIR=./deploy-data
ASTERERP_CONNECTION_STRING=Data Source=/app/data/astererp.db
```

修改数据库文件名时，连接串仍然必须使用容器内路径，例如：

```env
ASTERERP_DATA_DIR=/srv/project-manager/data
ASTERERP_CONNECTION_STRING=Data Source=/app/data/project-manager.db
```

后端通过 Compose 的 `ConnectionStrings__Default` 环境变量读取该配置，优先级高于 `appsettings.json`。

以下内容都会保存到 `ASTERERP_DATA_DIR`：

- 主数据库和 Hangfire 数据库；
- `application-databases/{tenantId}/{appCode}` 应用数据库；
- 文件上传目录；
- DataProtection keys；
- 分布式锁目录。

## 日志与排查

```powershell
docker compose logs -f backend
docker compose logs -f frontend
docker compose exec backend printenv ConnectionStrings__Default
```

修改 `.env` 后重新构建并启动：

```powershell
docker compose up -d --build
```
