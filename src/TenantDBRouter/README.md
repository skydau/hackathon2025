# TenantDBRouter

多租户数据库路由中间件，用于自动将请求路由到正确的租户数据库。

## 功能特性

- 从 HTTP 请求头 `X-Tenant-Id` 自动提取租户 ID
- 调用 Tenant Catalog Service 获取租户数据库配置
- 支持 Database-per-Tenant 和 Schema-per-Tenant 两种隔离模式
- 连接池管理和复用
- 租户 ID 缺失时抛出异常

## 安装

在 ASP.NET Core 项目中添加引用：

```xml
<ItemGroup>
  <ProjectReference Include="..\TenantDBRouter\TenantDBRouter.csproj" />
</ItemGroup>
```

## 使用方法

### 1. 注册服务

在 `Program.cs` 中注册 TenantDBRouter 服务：

```csharp
using TenantDBRouter.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 注册 TenantDBRouter，指定 Tenant Catalog Service 的基础 URL
builder.Services.AddTenantDBRouter("http://tenant-catalog-service:8080");

var app = builder.Build();

// 使用租户上下文中间件
app.UseTenantContext();

app.Run();
```

### 2. 在控制器中使用

```csharp
using TenantDBRouter;
using TenantDBRouter.Services;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly TenantDBRouter _dbRouter;
    private readonly ITenantContextAccessor _tenantContext;

    public TransactionsController(
        TenantDBRouter dbRouter,
        ITenantContextAccessor tenantContext)
    {
        _dbRouter = dbRouter;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetTransactions()
    {
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrEmpty(tenantId))
        {
            return BadRequest("Missing X-Tenant-Id header");
        }

        // 获取租户专属的数据库连接
        var connection = await _dbRouter.GetTenantConnectionAsync(tenantId);
        
        // 使用连接执行查询
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Transactions";
        
        // ... 执行查询并返回结果
        
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransaction([FromBody] TransactionRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrEmpty(tenantId))
        {
            return BadRequest("Missing X-Tenant-Id header");
        }

        // 使用 ExecuteQueryAsync 执行操作
        var result = await _dbRouter.ExecuteQueryAsync(
            tenantId,
            async (connection) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO Transactions (Amount, Description) VALUES (@Amount, @Description)";
                command.Parameters.AddWithValue("@Amount", request.Amount);
                command.Parameters.AddWithValue("@Description", request.Description);
                
                await command.ExecuteNonQueryAsync();
                return true;
            }
        );

        return Ok();
    }
}
```

## 数据库隔离模式

### Database-per-Tenant

每个租户拥有独立的数据库实例：

```json
{
  "Mode": "perDatabase",
  "Server": "XIA-JADU-LT\\SQLEXPRESS",
  "Database": "Tenant_A_DB",
  "CredentialRef": "vault-ref"
}
```

### Schema-per-Tenant

多个租户共享同一数据库，但使用不同的 schema：

```json
{
  "Mode": "perSchema",
  "Server": "XIA-JADU-LT\\SQLEXPRESS",
  "Database": "SharedDB",
  "Schema": "tenant_a",
  "CredentialRef": "vault-ref"
}
```

## 错误处理

当请求缺少 `X-Tenant-Id` 头时，会抛出 `TenantIdMissingException`：

```csharp
try
{
    var connection = await _dbRouter.GetTenantConnectionAsync(tenantId);
}
catch (TenantIdMissingException ex)
{
    // 处理租户 ID 缺失的情况
    return BadRequest("Tenant ID is required");
}
```

## 测试

项目包含完整的属性测试（Property-Based Tests），验证以下正确性属性：

- **属性 26**: DB Router 从请求上下文中提取租户 ID
- **属性 27**: DB Router 查询 Tenant Catalog 获取数据库配置
- **属性 28**: 连接池复用
- **属性 29**: 通过正确的连接池执行操作
- **属性 30**: 缺少租户 ID 时抛出错误
- **属性 31**: Database-per-Tenant 模式连接到租户专属数据库
- **属性 32**: Schema-per-Tenant 模式使用租户特定的 schema

运行测试：

```bash
dotnet test tests/TenantDBRouter.Tests/TenantDBRouter.Tests.csproj
```
