# Multi-Tenant Medical Platform

下一代多租户医疗后台系统 - 基于Kubernetes的云原生多租户平台

## 项目结构

```
MedLogicPlatform/
├── src/
│   ├── TenantCatalogService/      # 租户目录服务
│   ├── DeviceRegistryService/     # 设备注册服务
│   ├── AdminUI/                   # 管理界面 (Blazor)
│   └── SharedLibrary/             # 共享库
├── tests/
│   ├── TenantCatalogService.Tests/
│   └── DeviceRegistryService.Tests/
├── k8s/                           # Kubernetes部署清单
│   ├── namespace.yaml
│   ├── tenant-catalog-deployment.yaml
│   └── device-registry-deployment.yaml
└── .kiro/specs/                   # 规格文档
```

## 已完成功能

### 1. 项目基础架构 ✅

- ✅ .NET 9解决方案结构
- ✅ Tenant Catalog Service (ASP.NET Core Web API)
- ✅ Device Registry Service (ASP.NET Core Web API)
- ✅ SharedLibrary (共享库项目)
- ✅ Entity Framework Core配置
- ✅ Serilog日志记录
- ✅ 健康检查端点 (/health, /health/ready)
- ✅ Docker容器化配置
- ✅ Kubernetes部署清单
- ✅ 单元测试项目和基础测试

## 技术栈

- **.NET 9.0** - 应用框架
- **ASP.NET Core** - Web API
- **Entity Framework Core 9.0** - ORM
- **SQL Server** - 数据库
- **Serilog** - 结构化日志
- **Swagger/OpenAPI** - API文档
- **xUnit** - 单元测试
- **Docker** - 容器化
- **Kubernetes** - 编排

## 快速开始

### 构建项目

```bash
dotnet build
```

### 运行测试

```bash
# 运行所有测试
dotnet test

# 只运行DbContext测试
dotnet test --filter "FullyQualifiedName~DbContextTests"
```

### 运行服务

```bash
# Tenant Catalog Service
dotnet run --project src/TenantCatalogService

# Device Registry Service
dotnet run --project src/DeviceRegistryService

# Admin UI (管理界面)
dotnet run --project src/AdminUI
```

访问 Admin UI: http://localhost:5002

### 使用管理界面

Admin UI 提供了一个简单的 Web 界面用于:
- 创建和查看租户
- 注册设备并关联到租户
- 查看平台监控仪表盘
- 嵌入 Grafana 可视化

详细使用说明请参考: [Admin UI Quick Start](src/AdminUI/QUICK_START.md)

## 健康检查

两个服务都提供健康检查端点:

- `GET /health` - 基本健康检查
- `GET /health/ready` - 就绪检查

## Docker构建

```bash
# 构建Tenant Catalog Service
docker build -f src/TenantCatalogService/Dockerfile -t medlogic/tenant-catalog-service:latest .

# 构建Device Registry Service
docker build -f src/DeviceRegistryService/Dockerfile -t medlogic/device-registry-service:latest .
```

## Kubernetes部署

```bash
# 创建命名空间
kubectl apply -f k8s/namespace.yaml

# 部署服务
kubectl apply -f k8s/tenant-catalog-deployment.yaml
kubectl apply -f k8s/device-registry-deployment.yaml
```

## 下一步

参考 `.kiro/specs/multi-tenant-medical-platform/tasks.md` 查看完整的实施计划。

## 许可证

Copyright © 2025 TouchPoint Medical
