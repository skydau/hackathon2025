# Tenant Catalog 客户端

这个包提供了与 Tenant Catalog Service 通信的 Go 客户端。

## 功能特性

- **自动重试机制**: 在网络错误或服务器错误（5xx）时自动重试
- **指数退避**: 重试间隔采用指数退避策略（1秒、2秒、4秒）
- **超时控制**: 每个请求默认超时 30 秒
- **连接池管理**: 自动管理 HTTP 连接池，提高性能
- **上下文支持**: 支持 context.Context 用于取消和超时控制

## 使用方法

### 创建客户端

```go
import "github.com/touchpoint-medical/tenant-operator/pkg/catalog"

// 创建客户端，最多重试 3 次
client := catalog.NewTenantCatalogClient(
    "http://tenant-catalog.platform-system.svc.cluster.local:8080",
    3, // maxRetries
)
```

### 更新租户数据库配置

```go
ctx := context.Background()

config := catalog.DatabaseConfig{
    Server:      "host.minikube.internal,1433",
    Database:    "HospitalA_DB",
    Username:    "hospital_a_user",
    PasswordRef: "tenant-hospital-a-db-secret",
}

err := client.UpdateTenantDbConfig(ctx, "hospital-a", config)
if err != nil {
    log.Printf("Failed to update tenant database config: %v", err)
}
```

## 错误处理

客户端会自动处理以下情况：

- **网络错误**: 自动重试
- **5xx 服务器错误**: 自动重试
- **4xx 客户端错误**: 不重试，直接返回错误
- **上下文取消**: 立即停止重试并返回错误

## 配置说明

### 环境变量

在 Tenant Operator 的部署配置中设置以下环境变量：

```yaml
env:
  - name: TENANT_CATALOG_URL
    value: "http://tenant-catalog.platform-system.svc.cluster.local:8080"
```

### 重试策略

- 默认最大重试次数: 3 次
- 重试间隔: 指数退避（1秒、2秒、4秒）
- 总超时时间: 由 context 控制

### HTTP 客户端配置

- 请求超时: 30 秒
- 最大空闲连接: 100
- 每个主机的最大空闲连接: 10
- 空闲连接超时: 90 秒

## 接口定义

```go
type Client interface {
    UpdateTenantDbConfig(ctx context.Context, tenantID string, config DatabaseConfig) error
}

type DatabaseConfig struct {
    Server      string `json:"dbServer"`      // SQL Server 地址
    Database    string `json:"dbDatabase"`    // 数据库名称
    Username    string `json:"dbUsername"`    // 用户名
    PasswordRef string `json:"dbPasswordRef"` // Kubernetes Secret 引用
}
```

## 测试

客户端实现了 `catalog.Client` 接口，便于在测试中使用 mock：

```go
type MockCatalogClient struct {
    UpdateTenantDbConfigFunc func(ctx context.Context, tenantID string, config DatabaseConfig) error
}

func (m *MockCatalogClient) UpdateTenantDbConfig(ctx context.Context, tenantID string, config DatabaseConfig) error {
    if m.UpdateTenantDbConfigFunc != nil {
        return m.UpdateTenantDbConfigFunc(ctx, tenantID, config)
    }
    return nil
}
```

## 集成示例

在 Tenant Operator 的 main.go 中：

```go
// 初始化 Catalog 客户端
var catalogClient catalog.Client
if tenantCatalogURL != "" {
    catalogClient = catalog.NewTenantCatalogClient(tenantCatalogURL, 3)
}

// 初始化 Database Provisioner
dbProvisioner, err := database.NewDatabaseProvisioner(
    sqlServerUser,
    sqlServerPassword,
    sqlServerHost,
    catalogClient,
    mgr.GetClient(),
)
```

## 相关文档

- [Tenant Catalog Service API 文档](../../../src/TenantCatalogService/README.md)
- [Database Provisioner 文档](../database/README.md)
- [设计文档](../../../.kiro/specs/multi-tenant-medical-platform/design.md)
