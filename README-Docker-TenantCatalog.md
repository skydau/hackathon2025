# TenantCatalogService Docker 部署指南

本指南说明如何在 Docker 中运行 TenantCatalogService 和 SQL Server。

## 📋 前提条件

- Docker Desktop 已安装并运行
- Docker Compose 已安装
- 至少 4GB 可用内存（SQL Server 需要）

## 🚀 快速启动

### Windows (PowerShell)
```powershell
# 启动服务
.\scripts\docker-start-tenantcatalog.ps1

# 停止服务
.\scripts\docker-stop-tenantcatalog.ps1
```

### Linux/Mac (Bash)
```bash
# 给脚本执行权限
chmod +x scripts/docker-start-tenantcatalog.sh

# 启动服务
./scripts/docker-start-tenantcatalog.sh

# 停止服务
docker-compose -f docker-compose.tenantcatalog.yml down
```

### 手动启动
```bash
# 构建并启动所有服务
docker-compose -f docker-compose.tenantcatalog.yml up --build -d

# 查看服务状态
docker-compose -f docker-compose.tenantcatalog.yml ps

# 查看日志
docker-compose -f docker-compose.tenantcatalog.yml logs -f
```

## 🔗 服务端点

启动成功后，可以访问以下端点：

- **TenantCatalogService API**: http://localhost:8080
- **Swagger UI**: http://localhost:8080/swagger
- **健康检查**: http://localhost:8080/health
- **SQL Server**: localhost:1433 (sa/YourStrongPassword123!)

## 📊 服务架构

```
┌─────────────────┐    ┌──────────────────┐
│   AdminUI       │───▶│ TenantCatalog    │
│ (localhost:5093)│    │ (localhost:8080) │
└─────────────────┘    └──────────┬───────┘
                                  │
                       ┌──────────▼───────┐
                       │   SQL Server     │
                       │ (localhost:1433) │
                       └──────────────────┘
```

## 🛠️ 管理命令

### 查看服务状态
```bash
docker-compose -f docker-compose.tenantcatalog.yml ps
```

### 查看实时日志
```bash
# 所有服务日志
docker-compose -f docker-compose.tenantcatalog.yml logs -f

# 只看 TenantCatalog 日志
docker-compose -f docker-compose.tenantcatalog.yml logs -f tenant-catalog

# 只看 SQL Server 日志
docker-compose -f docker-compose.tenantcatalog.yml logs -f sqlserver
```

### 重启服务
```bash
# 重启所有服务
docker-compose -f docker-compose.tenantcatalog.yml restart

# 重启单个服务
docker-compose -f docker-compose.tenantcatalog.yml restart tenant-catalog
```

### 停止和清理
```bash
# 停止服务（保留数据）
docker-compose -f docker-compose.tenantcatalog.yml down

# 停止服务并清理数据
docker-compose -f docker-compose.tenantcatalog.yml down -v
```

## 🔧 配置说明

### 数据库配置
- **服务器**: sqlserver:1433 (容器内) / localhost:1433 (宿主机)
- **数据库**: MedLogicPlatform
- **用户名**: sa
- **密码**: YourStrongPassword123!

### 环境变量
可以在 `docker-compose.tenantcatalog.yml` 中修改以下环境变量：

```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Development
  - ConnectionStrings__DefaultConnection=Server=sqlserver,1433;Database=MedLogicPlatform;User Id=sa;Password=YourStrongPassword123!;TrustServerCertificate=True;Encrypt=True;
```

## 🧪 测试 API

### 健康检查
```bash
curl http://localhost:8080/health
```

### 获取所有租户
```bash
curl http://localhost:8080/api/tenants
```

### 创建租户
```bash
curl -X POST http://localhost:8080/api/tenants \
  -H "Content-Type: application/json" \
  -d '{
    "displayName": "测试医院",
    "dbConfig": {
      "mode": "perDatabase",
      "server": "sqlserver",
      "database": "TestHospital_DB",
      "credentialRef": "keyvault/test-hospital-password"
    },
    "throttling": {
      "rps": 100
    }
  }'
```

## 🐛 故障排除

### 服务无法启动
1. 检查 Docker 是否运行：`docker version`
2. 检查端口是否被占用：`netstat -an | findstr :8080`
3. 查看服务日志：`docker-compose -f docker-compose.tenantcatalog.yml logs`

### SQL Server 连接失败
1. 等待 SQL Server 完全启动（可能需要 1-2 分钟）
2. 检查 SQL Server 健康状态：`docker-compose -f docker-compose.tenantcatalog.yml ps`
3. 测试数据库连接：
   ```bash
   docker exec -it medlogic-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P YourStrongPassword123! -Q "SELECT 1"
   ```

### 内存不足
SQL Server 需要至少 2GB 内存。如果系统内存不足，可以：
1. 关闭其他应用程序
2. 增加 Docker Desktop 的内存限制
3. 使用轻量级数据库（如 SQLite）进行开发

## 📝 开发注意事项

1. **数据持久化**: 数据库数据存储在 Docker volume 中，停止容器不会丢失数据
2. **代码更改**: 修改代码后需要重新构建镜像：`docker-compose -f docker-compose.tenantcatalog.yml up --build`
3. **调试**: 可以通过 Visual Studio 或 VS Code 连接到运行中的容器进行调试