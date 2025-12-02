# 设计文档

## 概述

本设计文档描述了下一代多租户医疗后台系统的技术架构。该系统基于Kubernetes构建，采用云原生设计模式，为多个医院租户提供共享基础设施，同时确保严格的资源、网络和数据隔离。

系统的核心设计理念是：
- **声明式自动化**: 通过Kubernetes Operator模式实现租户生命周期的自动化管理
- **解耦与可扩展**: 设备、租户和数据库之间通过注册表和路由层解耦
- **纵深防御**: 在网络、身份、密钥和数据层构建多层安全隔离
- **精细化运营**: 支持按租户维度的监控、告警和成本核算

## 架构

### 系统架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                        外部设备层                                  │
│                   (medDispense Stations)                         │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                      智能网关层                                    │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Smart Gateway (NGINX/Envoy)                             │   │
│  │  - 设备识别                                                │   │
│  │  - 租户ID注入 (X-Tenant-Id)                               │   │
│  │  - 租户级速率限制                                           │   │
│  └──────────────────────────────────────────────────────────┘   │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                      应用服务层                                    │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ Tenant       │  │ Device       │  │ Backend      │          │
│  │ Catalog      │  │ Registry     │  │ Microservice │          │
│  │ Service      │  │ Service      │  │ (medLogic)   │          │
│  └──────────────┘  └──────────────┘  └──────┬───────┘          │
│                                              │                   │
│                                              ▼                   │
│                                    ┌──────────────────┐          │
│                                    │  DB Router       │          │
│                                    │  Middleware      │          │
│                                    └──────┬───────────┘          │
└───────────────────────────────────────────┼──────────────────────┘
                                            │
                                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                      数据存储层                                    │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ Tenant A DB  │  │ Tenant B DB  │  │ Tenant C DB  │          │
│  │ (Isolated)   │  │ (Isolated)   │  │ (Isolated)   │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    控制平面与安全层                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ Tenant       │  │ Azure Key    │  │ OPA          │          │
│  │ Operator     │  │ Vault        │  │ Gatekeeper   │          │
│  │ (K8s CRD)    │  │ (Secrets)    │  │ (Policy)     │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                      可观测性层                                    │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ Prometheus   │  │ Loki         │  │ Grafana      │          │
│  │ (Metrics)    │  │ (Logs)       │  │ (Dashboard)  │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
└─────────────────────────────────────────────────────────────────┘
```

### 请求流程

1. **设备请求到达**: medDispense Station发送HTTP请求到Smart Gateway
2. **设备识别**: Smart Gateway从请求中提取deviceId，调用Device Registry查询对应的tenantId
3. **租户注入**: Smart Gateway将tenantId作为X-Tenant-Id头注入请求
4. **速率限制**: Smart Gateway基于X-Tenant-Id应用租户级速率限制
5. **服务路由**: 请求被转发到后端微服务
6. **数据库路由**: DB Router从X-Tenant-Id提取租户信息，调用Tenant Catalog获取数据库配置
7. **数据操作**: DB Router通过租户专属连接池执行数据库操作
8. **响应返回**: 结果沿原路返回到设备

## 组件与接口

### 1. Tenant Catalog Service

租户目录服务是所有租户元数据的权威来源。

**技术栈**: .NET 8/9 + ASP.NET Core Web API + Entity Framework Core + SQL Server

**数据模型**:
```csharp
public class Tenant
{
    public string Id { get; set; }                    // 唯一租户ID
    public string DisplayName { get; set; }           // 显示名称
    public TenantStatus Status { get; set; }          // 状态
    public DatabaseConfig DbConfig { get; set; }      // 数据库配置
    public ThrottlingConfig Throttling { get; set; }  // 速率限制配置
    public SLOConfig Slo { get; set; }                // SLO配置
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum TenantStatus
{
    Provisioning,
    Enabled,
    Disabled,
    Decommissioned
}

public class DatabaseConfig
{
    public string Mode { get; set; }          // "perDatabase" 或 "perSchema"
    public string Server { get; set; }
    public string Database { get; set; }
    public string? Schema { get; set; }
    public string CredentialRef { get; set; }  // Azure Key Vault引用
}

public class ThrottlingConfig
{
    public int Rps { get; set; }  // 每秒请求数限制
}

public class SLOConfig
{
    public string Availability { get; set; }      // 如 "99.9%"
    public int P95LatencyMs { get; set; }         // p95延迟目标(毫秒)
}
```

**REST API**:
- `POST /api/tenants` - 创建新租户
- `GET /api/tenants/{id}` - 获取租户详情
- `PATCH /api/tenants/{id}` - 更新租户
- `GET /api/tenants/{id}/db-config` - 获取数据库配置（供DB Router调用）

### 2. Tenant Operator

基于Kubernetes Operator模式的控制器，监听Tenant CRD变化并自动编排资源。

**技术栈**: Go + controller-runtime

**Tenant CRD定义**:
```yaml
apiVersion: tenants.medlogic.io/v1
kind: Tenant
metadata:
  name: hospital-a
spec:
  displayName: "Hospital A"
  db:
    mode: perDatabase
    server: sqlserver.default.svc.cluster.local
    database: HospitalA_DB
  throttling:
    rps: 100
  slo:
    availability: "99.9%"
    p95_latency_ms: 1000
status:
  phase: Provisioning | Ready | Failed
  conditions: []
  namespaceCreated: true
  resourcesProvisioned: true
```

**Operator职责**:
1. 监听Tenant CRD的创建、更新、删除事件
2. 创建租户专属命名空间（如`tenant-hospital-a`）
3. 在命名空间内创建ConfigMap和Secret引用
4. 应用ResourceQuota限制资源使用
5. 应用NetworkPolicy实现网络隔离
6. 配置RBAC规则
7. 在Azure Key Vault中创建/删除租户密钥
8. 更新Tenant CRD的status字段

### 3. Device Registry Service

维护设备到租户的映射关系。

**技术栈**: .NET 8/9 + ASP.NET Core Web API + Entity Framework Core + SQL Server

**数据模型**:
```csharp
public class Device
{
    public string SerialNumber { get; set; }  // 设备序列号（主键）
    public string TenantId { get; set; }      // 关联的租户ID
    public string DeviceType { get; set; }    // 设备类型（如 "medDispense"）
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
}
```

**REST API**:
- `POST /api/devices` - 注册新设备
- `GET /api/devices/{serialNumber}` - 获取设备信息
- `GET /api/devices/{serialNumber}/tenant` - 查询设备的租户ID（供Smart Gateway调用）
- `PATCH /api/devices/{serialNumber}` - 更新设备信息

### 4. Smart Gateway

基于NGINX或Envoy实现的智能网关。

**技术栈**: NGINX + Lua 或 Envoy + WASM

**核心功能**:
1. 从请求中提取deviceId（从HTTP头或请求体）
2. 调用Device Registry的`GET /devices/{serialNumber}/tenant`接口
3. 将返回的tenantId注入为`X-Tenant-Id`请求头
4. 基于`X-Tenant-Id`应用租户级速率限制
5. 转发请求到后端服务

**NGINX配置示例**:
```nginx
http {
    # 定义租户级速率限制区域
    limit_req_zone $http_x_tenant_id zone=tenant_rl:20m rate=50r/s;
    
    # 上游Device Registry服务
    upstream device_registry {
        server device-registry.default.svc.cluster.local:8080;
    }
    
    # 上游后端服务
    upstream backend_service {
        server medlogic-service.default.svc.cluster.local:8080;
    }
    
    server {
        listen 80;
        
        location /api {
            # Lua脚本：查询Device Registry并注入X-Tenant-Id
            access_by_lua_block {
                local http = require "resty.http"
                local httpc = http.new()
                
                -- 从请求头提取deviceId
                local device_id = ngx.var.http_device_id
                if not device_id then
                    ngx.status = 400
                    ngx.say("Missing Device-Id header")
                    return ngx.exit(400)
                end
                
                -- 查询Device Registry
                local res, err = httpc:request_uri(
                    "http://device-registry:8080/devices/" .. device_id .. "/tenant",
                    { method = "GET" }
                )
                
                if not res or res.status ~= 200 then
                    ngx.status = 401
                    ngx.say("Device not authorized")
                    return ngx.exit(401)
                end
                
                -- 解析tenantId并注入请求头
                local cjson = require "cjson"
                local data = cjson.decode(res.body)
                ngx.req.set_header("X-Tenant-Id", data.tenantId)
            }
            
            # 应用租户级速率限制
            limit_req zone=tenant_rl burst=100 nodelay;
            
            # 转发到后端
            proxy_pass http://backend_service;
            proxy_set_header X-Tenant-Id $http_x_tenant_id;
        }
    }
}
```

### 5. DB Router Middleware

嵌入在后端微服务中的数据库路由中间件。

**技术栈**: .NET 8/9 NuGet包

**核心接口**:
```csharp
public class TenantDBRouter
{
    private readonly ConcurrentDictionary<string, SqlConnection> _pools;
    private readonly ITenantCatalogClient _tenantCatalogClient;
    
    public async Task<SqlConnection> GetTenantConnectionAsync(string tenantId)
    {
        // 检查是否已有连接
        if (_pools.TryGetValue(tenantId, out var existingConnection))
        {
            return existingConnection;
        }
        
        // 从Tenant Catalog获取数据库配置
        var dbConfig = await _tenantCatalogClient.GetDbConfigAsync(tenantId);
        
        // 创建新连接
        var connectionString = BuildConnectionString(dbConfig);
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        
        _pools.TryAdd(tenantId, connection);
        return connection;
    }
    
    public async Task<T> ExecuteQueryAsync<T>(string tenantId, string query, object parameters)
    {
        var connection = await GetTenantConnectionAsync(tenantId);
        // 使用Dapper或EF Core执行查询
        return await connection.QueryFirstOrDefaultAsync<T>(query, parameters);
    }
}
```

**使用示例**:
```csharp
// 在ASP.NET Core中间件中提取tenantId
app.Use(async (context, next) =>
{
    if (!context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantId))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { error = "Missing X-Tenant-Id header" });
        return;
    }
    
    context.Items["TenantId"] = tenantId.ToString();
    await next();
});

// 在Controller中使用
[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly TenantDBRouter _dbRouter;
    
    [HttpPost]
    public async Task<IActionResult> CreateTransaction([FromBody] TransactionRequest request)
    {
        var tenantId = HttpContext.Items["TenantId"] as string;
        
        var result = await _dbRouter.ExecuteQueryAsync<Transaction>(
            tenantId,
            "INSERT INTO Transactions (Amount, Description) OUTPUT INSERTED.* VALUES (@Amount, @Description)",
            new { request.Amount, request.Description }
        );
        
        return CreatedAtAction(nameof(GetTransaction), new { id = result.Id }, result);
    }
}
```

## 数据模型

### Tenant Catalog数据库

**Tenants表**:
```sql
CREATE TABLE Tenants (
    Id NVARCHAR(50) PRIMARY KEY,
    DisplayName NVARCHAR(200) NOT NULL,
    Status NVARCHAR(50) NOT NULL,
    DbMode NVARCHAR(20) NOT NULL,
    DbServer NVARCHAR(200) NOT NULL,
    DbDatabase NVARCHAR(100) NOT NULL,
    DbSchema NVARCHAR(100),
    DbCredentialRef NVARCHAR(200) NOT NULL,
    ThrottlingRps INT NOT NULL DEFAULT 100,
    SloAvailability NVARCHAR(20),
    SloP95LatencyMs INT,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_Tenants_Status ON Tenants(Status);
```

### Device Registry数据库

**Devices表**:
```sql
CREATE TABLE Devices (
    SerialNumber NVARCHAR(100) PRIMARY KEY,
    TenantId NVARCHAR(50) NOT NULL,
    DeviceType NVARCHAR(50) NOT NULL,
    RegisteredAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastSeenAt DATETIME2,
    FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);

CREATE INDEX IX_Devices_TenantId ON Devices(TenantId);
```

### 租户数据库（示例）

每个租户的数据库包含业务表，例如：

**Transactions表**:
```sql
CREATE TABLE Transactions (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Amount DECIMAL(18,2) NOT NULL,
    Description NVARCHAR(500),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

## 正确性属性

*属性是一个特征或行为，应该在系统的所有有效执行中保持为真。属性是人类可读规范和机器可验证正确性保证之间的桥梁。*


### 租户管理属性

**属性 1: 租户创建返回唯一ID**
*对于任何*有效的租户创建请求，Tenant Catalog Service应该返回一个唯一的租户ID，且该ID不与系统中任何现有租户ID冲突。
**验证需求: 1.1**

**属性 2: 租户数据往返一致性**
*对于任何*成功创建的租户，通过GET端点查询该租户应该返回与创建时完全一致的元数据（名称、数据库配置等）。
**验证需求: 1.2**

**属性 3: 租户状态更新反映正确**
*对于任何*租户和任何有效的状态值，更新租户状态后再次查询应该返回更新后的状态。
**验证需求: 1.3**

**属性 4: 新租户初始状态一致**
*对于任何*新创建的租户，其初始状态应该始终为"Provisioning"。
**验证需求: 1.4**

**属性 5: 不存在的租户返回404**
*对于任何*不存在于系统中的租户ID，查询请求应该返回404错误响应。
**验证需求: 1.5**

### 租户资源编排属性

**属性 6: CRD创建触发命名空间创建**
*对于任何*新创建的Tenant CRD资源，Tenant Operator应该自动创建对应名称的Kubernetes命名空间。
**验证需求: 2.1**

**属性 7: 命名空间包含配置ConfigMap**
*对于任何*由Tenant Operator创建的命名空间，该命名空间内应该存在包含租户配置的ConfigMap。
**验证需求: 2.2**

**属性 8: 命名空间应用资源配额**
*对于任何*由Tenant Operator创建的命名空间，该命名空间应该有ResourceQuota对象限制CPU和内存使用。
**验证需求: 2.3**

**属性 9: 命名空间应用网络策略**
*对于任何*由Tenant Operator创建的命名空间，该命名空间应该有NetworkPolicy对象默认拒绝跨命名空间流量。
**验证需求: 2.4**

**属性 10: 命名空间配置RBAC**
*对于任何*由Tenant Operator创建的命名空间，该命名空间应该有RoleBinding对象限制访问权限。
**验证需求: 2.5**

### 设备注册与映射属性

**属性 11: 设备注册创建映射**
*对于任何*有效的设备序列号和租户ID组合，注册请求应该成功创建设备到租户的映射记录。
**验证需求: 3.1**

**属性 12: 设备查询往返一致性**
*对于任何*已注册的设备，通过序列号查询应该返回注册时关联的租户ID。
**验证需求: 3.2**

**属性 13: 未注册设备返回错误**
*对于任何*未在Device Registry中注册的设备序列号，查询请求应该返回错误响应。
**验证需求: 3.3**

**属性 14: 设备租户关联可更新**
*对于任何*已注册的设备和任何有效的新租户ID，更新请求应该成功修改设备的租户关联。
**验证需求: 3.4**

**属性 15: 重复注册返回冲突**
*对于任何*已存在的设备序列号，尝试再次注册应该返回冲突错误响应。
**验证需求: 3.5**

### 智能网关路由属性

**属性 16: 网关提取设备标识**
*对于任何*包含设备标识的入站请求，Smart Gateway应该能够正确提取设备序列号。
**验证需求: 4.1**

**属性 17: 网关查询设备注册表**
*对于任何*提取到设备序列号的请求，Smart Gateway应该调用Device Registry查询租户ID。
**验证需求: 4.2**

**属性 18: 网关注入租户ID头**
*对于任何*成功查询到租户ID的请求，Smart Gateway应该将租户ID作为X-Tenant-Id头注入到转发的请求中。
**验证需求: 4.3**

**属性 19: 网关转发请求到后端**
*对于任何*注入了X-Tenant-Id头的请求，Smart Gateway应该将请求转发到内部微服务。
**验证需求: 4.4**

**属性 20: 未授权设备返回401**
*对于任何*未在Device Registry中找到的设备，Smart Gateway应该拒绝请求并返回401错误。
**验证需求: 4.5**

### 流量控制属性

**属性 21: 租户级速率限制应用**
*对于任何*带有X-Tenant-Id头的请求，Smart Gateway应该基于该租户ID应用对应的速率限制策略。
**验证需求: 5.1**

**属性 22: 超速请求返回429**
*对于任何*租户，当其请求速率超过配置的限制时，超出的请求应该被拒绝并返回429错误。
**验证需求: 5.2**

**属性 23: 租户隔离不受影响**
*对于任何*两个不同的租户A和B，当租户A的请求被速率限制时，租户B的请求应该继续正常处理。
**验证需求: 5.3**

**属性 24: 租户特定速率限制生效**
*对于任何*在Tenant CRD中配置了throttling参数的租户，Smart Gateway应该应用该租户特定的速率限制值而非默认值。
**验证需求: 5.4**

**属性 25: 默认速率限制应用**
*对于任何*不包含X-Tenant-Id头的请求，Smart Gateway应该应用默认的速率限制策略。
**验证需求: 5.5**

### 数据库路由属性

**属性 26: DB Router提取租户ID**
*对于任何*包含X-Tenant-Id头的请求上下文，DB Router应该能够正确提取租户ID。
**验证需求: 6.1**

**属性 27: DB Router查询数据库配置**
*对于任何*提取到的租户ID，DB Router应该调用Tenant Catalog Service获取该租户的数据库配置。
**验证需求: 6.2**

**属性 28: 连接池复用**
*对于任何*租户，当多次请求该租户的数据库连接时，DB Router应该复用已存在的连接池而不是创建新的。
**验证需求: 6.3**

**属性 29: 通过正确连接池执行操作**
*对于任何*租户的数据库操作，DB Router应该通过该租户专属的连接池执行操作。
**验证需求: 6.4**

**属性 30: 缺少租户ID抛出错误**
*对于任何*请求上下文中缺少租户ID的情况，DB Router应该抛出错误并拒绝执行数据库操作。
**验证需求: 6.5**

### 数据库隔离策略属性

**属性 31: Database-per-Tenant模式连接专属数据库**
*对于任何*配置为Database-per-Tenant模式的租户，DB Router应该连接到该租户的专属数据库实例。
**验证需求: 7.1**

**属性 32: Schema-per-Tenant模式使用租户模式**
*对于任何*配置为Schema-per-Tenant模式的租户，DB Router应该连接到共享数据库并使用租户特定的schema。
**验证需求: 7.2**

**属性 33: Operator根据模式创建资源**
*对于任何*在Tenant CRD中指定数据库模式的租户，Tenant Operator应该根据该模式创建相应的数据库资源。
**验证需求: 7.3**

**属性 34: Database-per-Tenant连接池独立**
*对于任何*两个使用Database-per-Tenant模式的租户，它们的连接池对象应该完全独立。
**验证需求: 7.4**

**属性 35: Schema-per-Tenant自动添加前缀**
*对于任何*使用Schema-per-Tenant模式的租户，DB Router生成的SQL查询应该自动包含该租户的schema前缀。
**验证需求: 7.5**

### 网络隔离属性

**属性 36: 命名空间默认拒绝策略**
*对于任何*由Tenant Operator创建的租户命名空间，应该应用NetworkPolicy默认拒绝所有入站和跨命名空间流量。
**验证需求: 8.1**

**属性 37: 跨租户访问被拒绝**
*对于任何*两个不同租户的命名空间，从一个命名空间的Pod尝试访问另一个命名空间的服务应该被NetworkPolicy拒绝。
**验证需求: 8.2**

**属性 38: 共享服务访问允许**
*对于任何*租户命名空间，NetworkPolicy应该明确允许到共享服务命名空间的出站流量。
**验证需求: 8.3**

**属性 39: 命名空间内通信允许**
*对于任何*租户命名空间内的任意两个Pod，它们应该能够相互通信。
**验证需求: 8.4**

**属性 40: 网络策略更新立即生效**
*对于任何*租户的NetworkPolicy更新，Kubernetes应该立即应用新的网络规则。
**验证需求: 8.5**

### 密钥管理属性

**属性 41: 租户创建时创建密钥**
*对于任何*新创建的租户，Tenant Operator应该在Azure Key Vault中创建该租户的数据库凭据密钥。
**验证需求: 9.1**

**属性 42: Pod启动时挂载密钥**
*对于任何*微服务Pod启动，Secrets Store CSI Driver应该将租户的密钥从Azure Key Vault挂载为Pod内的文件。
**验证需求: 9.2**

**属性 43: 密钥轮换自动更新**
*对于任何*在Azure Key Vault中被轮换的密钥，Secrets Store CSI Driver应该自动更新Pod内挂载的密钥文件。
**验证需求: 9.3**

**属性 44: 使用Workload Identity访问**
*对于任何*微服务访问数据库的操作，应该使用AKS Workload Identity进行无凭据身份验证。
**验证需求: 9.4**

**属性 45: 租户删除时删除密钥**
*对于任何*被删除的租户，Tenant Operator应该吊销并删除该租户在Azure Key Vault中的所有密钥。
**验证需求: 9.5**

### 数据加密属性

**属性 46: 租户数据库启用TDE**
*对于任何*新创建的租户数据库，系统应该启用SQL Server透明数据加密(TDE)。
**验证需求: 10.1**

**属性 47: 敏感字段使用Always Encrypted**
*对于任何*被标记为高敏感度的数据字段，系统应该使用Always Encrypted在客户端加密数据。
**验证需求: 10.3**

**属性 48: 加密数据访问控制**
*对于任何*使用Always Encrypted加密的数据，只有持有正确密钥的客户端应该能够解密数据。
**验证需求: 10.4**

**属性 49: 备份文件加密**
*对于任何*租户数据库的备份，备份文件应该是加密的。
**验证需求: 10.5**

### 可观测性属性

**属性 50: 指标包含租户标签**
*对于任何*系统发出的Prometheus指标，该指标应该包含tenant_id标签。
**验证需求: 11.1**

**属性 51: 日志包含租户字段**
*对于任何*系统写入的日志条目，该日志应该包含tenant_id字段。
**验证需求: 11.2**

**属性 52: Grafana支持租户过滤**
*对于任何*在Grafana中的指标查询，应该支持按tenant_id过滤和聚合数据。
**验证需求: 11.3**

**属性 53: 日志请求包含租户头**
*对于任何*发送到Loki的日志，请求应该在头中包含X-Scope-OrgID以实现租户级隔离。
**验证需求: 11.4**

**属性 54: 日志查询租户隔离**
*对于任何*特定租户的日志查询，Loki应该只返回该租户的日志条目。
**验证需求: 11.5**

### SLO监控属性

**属性 55: SLO配置存储**
*对于任何*在Tenant CRD中定义SLO参数的租户，系统应该存储该租户的可用性和延迟目标。
**验证需求: 12.1**

**属性 56: SLO达成率计算**
*对于任何*租户的性能指标，系统应该计算该租户的实际SLO达成率。
**验证需求: 12.2**

**属性 57: 错误率告警触发**
*对于任何*租户，当其错误率超过SLO定义的阈值时，Prometheus应该触发告警通知。
**验证需求: 12.3**

**属性 58: 延迟告警触发**
*对于任何*租户，当其p95延迟超过SLO定义的阈值时，Prometheus应该触发告警通知。
**验证需求: 12.4**

**属性 59: 告警包含详细信息**
*对于任何*触发的SLO告警，告警消息应该包含租户ID和具体的SLO违规详情。
**验证需求: 12.5**

### 租户退服属性

**属性 60: 退服状态触发流程**
*对于任何*租户状态被更新为"Decommissioned"的情况，Tenant Operator应该开始执行退服流程。
**验证需求: 13.1**

**属性 61: 退服首先创建备份**
*对于任何*开始退服流程的租户，Tenant Operator应该首先创建租户数据库的完整备份。
**验证需求: 13.2**

**属性 62: 备份后吊销密钥**
*对于任何*完成数据备份的退服租户，Tenant Operator应该吊销该租户在Azure Key Vault中的所有密钥。
**验证需求: 13.3**

**属性 63: 密钥吊销后删除命名空间**
*对于任何*密钥被吊销的退服租户，Tenant Operator应该删除租户的Kubernetes命名空间及其所有资源。
**验证需求: 13.4**

**属性 64: 退服保留审计日志**
*对于任何*完成退服流程的租户，Tenant Operator应该保留审计日志记录退服操作的详细信息。
**验证需求: 13.5**

### 策略合规属性

**属性 65: Gatekeeper验证租户标签**
*对于任何*尝试部署到租户命名空间的工作负载，OPA Gatekeeper应该验证该工作负载的元数据包含tenantId标签。
**验证需求: 14.1**

**属性 66: 缺少标签拒绝部署**
*对于任何*缺少必需tenantId标签的工作负载，OPA Gatekeeper应该拒绝部署请求并返回明确的错误消息。
**验证需求: 14.2**

**属性 67: 跨租户策略被拒绝**
*对于任何*尝试创建跨租户NetworkPolicy的请求，OPA Gatekeeper应该拒绝该策略并返回违规说明。
**验证需求: 14.3**

**属性 68: 策略更新立即生效**
*对于任何*在OPA Gatekeeper中更新的策略规则，Gatekeeper应该立即应用新规则到后续的部署请求。
**验证需求: 14.4**

**属性 69: 符合策略允许部署**
*对于任何*符合所有策略要求的部署请求，OPA Gatekeeper应该允许请求通过并继续部署流程。
**验证需求: 14.5**

### 成本计量属性

**属性 70: 资源使用指标记录**
*对于任何*租户的工作负载，系统应该记录该租户的CPU使用量、内存占用、存储用量和网络流量。
**验证需求: 15.1**

**属性 71: 成本计算公式正确**
*对于任何*租户的资源使用量，系统应该将使用量乘以预设的费率得出正确的估算成本。
**验证需求: 15.2**

**属性 72: 月度报告包含明细**
*对于任何*租户的月度成本报告，报告应该包含各项资源成本的明细。
**验证需求: 15.3**

**属性 73: 配额限制强制执行**
*对于任何*租户，当其资源使用超过ResourceQuota限制时，Kubernetes应该阻止该租户继续分配资源。
**验证需求: 15.4**

**属性 74: 历史成本数据查询**
*对于任何*租户和任何有效的时间范围，系统应该返回该时间范围内的成本趋势数据。
**验证需求: 15.5**

## 错误处理

### 错误分类

系统定义以下错误类别：

1. **客户端错误 (4xx)**
   - 400 Bad Request: 请求格式错误或缺少必需参数
   - 401 Unauthorized: 设备未授权或身份验证失败
   - 404 Not Found: 请求的资源不存在
   - 409 Conflict: 资源冲突（如重复注册）
   - 429 Too Many Requests: 超过速率限制

2. **服务器错误 (5xx)**
   - 500 Internal Server Error: 服务内部错误
   - 503 Service Unavailable: 服务暂时不可用

### 错误响应格式

所有API错误响应应遵循统一格式：

```csharp
public class ErrorResponse
{
    public ErrorDetail Error { get; set; }
}

public class ErrorDetail
{
    public string Code { get; set; }           // 错误代码
    public string Message { get; set; }        // 人类可读的错误消息
    public object? Details { get; set; }       // 可选的详细信息
    public string? TenantId { get; set; }      // 相关的租户ID（如适用）
    public DateTime Timestamp { get; set; }    // 时间戳
    public string RequestId { get; set; }      // 请求追踪ID
}
```

### 错误处理策略

1. **重试机制**: 对于临时性错误（如网络超时、503错误），客户端应实现指数退避重试
2. **熔断器**: 当下游服务持续失败时，应用熔断器模式防止级联故障
3. **降级策略**: 关键路径应有降级方案，如缓存、默认值等
4. **错误日志**: 所有错误应记录详细日志，包含租户ID、请求ID和堆栈跟踪

## 测试策略

### 单元测试

单元测试验证单个组件的功能正确性：

- **Tenant Catalog Service**: 测试CRUD操作、数据验证、错误处理
- **Device Registry Service**: 测试设备注册、查询、更新逻辑
- **DB Router Middleware**: 测试连接管理、租户ID提取、配置查询
- **Tenant Operator**: 测试CRD事件处理、资源创建逻辑

**工具**: xUnit + FluentAssertions + Moq (.NET), Go testing package (Operator)

### 属性测试

属性测试使用属性测试框架验证上述定义的正确性属性：

**工具**: 
- **.NET**: FsCheck (可与xUnit集成)
- **Go**: gopter (用于Operator)

**配置**: 每个属性测试应运行至少100次迭代

**标记格式**: 每个属性测试必须使用注释标记其对应的设计文档属性：
```csharp
// **Feature: multi-tenant-medical-platform, Property 1: 租户创建返回唯一ID**
[Property(MaxTest = 100)]
public Property TenantCreationReturnsUniqueIds()
{
    return Prop.ForAll(
        Arb.From<TenantCreateRequest>(),
        async tenantData =>
        {
            // 测试逻辑
            var result = await _tenantService.CreateTenantAsync(tenantData);
            return result.Id != null && IsUnique(result.Id);
        }
    );
}
```

### 集成测试

集成测试验证组件之间的交互：

- **端到端请求流**: 从Smart Gateway到DB Router的完整请求链路
- **Operator集成**: Tenant Operator与Kubernetes API的交互
- **密钥管理集成**: 与Azure Key Vault的集成

**工具**: WebApplicationFactory (ASP.NET Core集成测试), Testcontainers (.NET), kind (本地Kubernetes集群)

### 安全测试

- **网络隔离测试**: 验证NetworkPolicy有效阻止跨租户访问
- **密钥轮换测试**: 验证密钥轮换不影响服务可用性
- **策略合规测试**: 验证OPA Gatekeeper正确执行策略

### 性能测试

- **速率限制测试**: 验证租户级速率限制正确工作
- **连接池性能**: 验证DB Router连接池复用效率
- **并发租户测试**: 验证系统在多租户并发场景下的性能

**工具**: k6 (负载测试)

## 部署架构

### Kubernetes资源组织

```
Cluster
├── Namespace: platform-system
│   ├── Deployment: tenant-catalog-service
│   ├── Deployment: device-registry-service
│   ├── Deployment: tenant-operator
│   ├── Service: tenant-catalog
│   └── Service: device-registry
├── Namespace: gateway
│   ├── Deployment: smart-gateway (NGINX)
│   └── Service: smart-gateway (LoadBalancer)
├── Namespace: tenant-hospital-a
│   ├── Deployment: medlogic-service
│   ├── ConfigMap: tenant-config
│   ├── ResourceQuota: tenant-quota
│   └── NetworkPolicy: tenant-isolation
├── Namespace: tenant-hospital-b
│   └── ...
└── Namespace: observability
    ├── Deployment: prometheus
    ├── Deployment: loki
    └── Deployment: grafana
```

### 高可用性配置

- **多副本部署**: 所有关键服务至少3个副本
- **Pod反亲和性**: 确保副本分布在不同节点
- **健康检查**: 配置liveness和readiness探针
- **自动扩缩容**: 基于CPU/内存使用率的HPA

### 灾难恢复

- **数据库备份**: 每日自动备份所有租户数据库
- **配置备份**: 定期备份Kubernetes资源定义
- **恢复演练**: 季度进行灾难恢复演练

## 监控与告警

### 关键指标

**平台级指标**:
- 总请求数、错误率、p95/p99延迟
- 活跃租户数、总设备数
- 资源使用率（CPU、内存、存储）

**租户级指标**:
- 每租户的请求数、错误率、延迟
- 每租户的资源消耗
- 每租户的SLO达成率

### 告警规则

1. **高错误率告警**: 任何租户错误率 > 5% 持续5分钟
2. **高延迟告警**: 任何租户p95延迟 > SLO阈值持续5分钟
3. **资源配额告警**: 任何租户资源使用 > 80%配额
4. **服务不可用告警**: 任何核心服务副本数 < 2

### 日志聚合

- 所有日志统一发送到Loki
- 日志包含结构化字段：tenantId, requestId, level, message
- 支持按租户、时间范围、日志级别查询

## 安全考虑

### 威胁模型

1. **跨租户数据泄露**: 通过网络隔离、数据库隔离、访问控制防护
2. **凭据泄露**: 通过Key Vault、Workload Identity消除明文凭据
3. **拒绝服务攻击**: 通过速率限制、资源配额防护
4. **未授权访问**: 通过设备注册、身份验证、RBAC防护

### 合规要求

- **HIPAA**: 数据加密（TDE、Always Encrypted）、审计日志、访问控制
- **GDPR**: 数据隔离、数据删除（退服流程）、访问日志
- **SOC 2**: 监控告警、变更管理、灾难恢复

### 安全最佳实践

- 最小权限原则：所有服务账号仅授予必需权限
- 定期密钥轮换：每90天轮换数据库凭据
- 安全扫描：定期扫描容器镜像和依赖漏洞
- 网络分段：使用NetworkPolicy严格限制流量

## 可扩展性考虑

### 水平扩展

- **无状态服务**: Tenant Catalog、Device Registry、Smart Gateway均为无状态，可水平扩展
- **数据库分片**: 支持Database-per-Tenant模式天然支持分片
- **缓存层**: 可引入Redis缓存租户配置和设备映射

### 垂直扩展

- **资源配额调整**: 可根据租户需求动态调整ResourceQuota
- **数据库升级**: 支持为高负载租户升级到更大的数据库实例

### 性能优化

- **连接池优化**: DB Router维护租户级连接池，避免频繁建立连接
- **配置缓存**: 缓存租户配置减少对Tenant Catalog的查询
- **异步处理**: Tenant Operator使用工作队列异步处理CRD事件

## 未来增强

1. **多区域部署**: 支持跨Azure区域的租户部署
2. **自动扩缩容**: 基于租户负载自动调整资源
3. **高级路由**: 支持基于地理位置、服务等级的智能路由
4. **AI驱动的异常检测**: 使用机器学习检测异常租户行为
5. **自助服务门户**: 租户可自助管理设备、查看监控、下载报告
