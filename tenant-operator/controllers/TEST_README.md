# 属性测试说明

## 任务 6.7: Operator自动创建租户数据库

### 测试描述
**属性 33: Operator自动创建租户数据库**
**验证需求: 7.3**

此测试验证Tenant Operator在接收到Database-per-Tenant模式的Tenant CRD时，能够自动创建租户数据库。

### 测试实现
测试位于：`tenant-operator/controllers/tenant_controller_test.go`

函数名：`TestProperty_OperatorAutoCreatesDatabase`

### 测试内容
1. 使用gopter生成100个随机Tenant实例
2. 对每个Tenant实例：
   - 确保配置为Database-per-Tenant模式
   - 调用Reconcile方法
   - 验证DatabaseProvisioner.ProvisionDatabase被调用
   - 验证数据库名称正确生成
   - 验证Tenant CRD的status.databaseCreated字段被设置为true
   - 验证Kubernetes Secret被创建，包含数据库凭据
   - 验证Secret包含所有必需字段（username, password, server, database）

### Mock实现
测试使用`MockDatabaseProvisioner`来模拟真实的数据库创建过程：
- 跟踪ProvisionDatabase方法的调用
- 记录创建的数据库名称
- 创建Kubernetes Secret（与真实实现一致）

### 运行测试

#### 前置条件
- 安装Go 1.21或更高版本
- 安装项目依赖：`go mod download`

#### 运行命令
```bash
# 进入tenant-operator目录
cd tenant-operator

# 运行特定测试
go test -v ./controllers -run TestProperty_OperatorAutoCreatesDatabase

# 运行所有属性测试
go test -v ./controllers -run TestProperty

# 运行所有测试
go test -v ./controllers
```

### 测试输出
成功的测试输出应该显示：
```
=== RUN   TestProperty_OperatorAutoCreatesDatabase
+ For any Tenant with Database-per-Tenant mode, Operator should automatically create the database on SQL Server: OK, passed 100 tests.
--- PASS: TestProperty_OperatorAutoCreatesDatabase (X.XXs)
PASS
```

### 注意事项
1. 此测试使用fake Kubernetes client，不需要真实的Kubernetes集群
2. 此测试使用MockDatabaseProvisioner，不需要真实的SQL Server连接
3. 测试验证的是Operator的逻辑流程，而不是实际的数据库操作
4. 真实的数据库创建功能在`tenant-operator/pkg/database/provisioner.go`中实现

### 相关文件
- 测试文件：`tenant-operator/controllers/tenant_controller_test.go`
- 控制器实现：`tenant-operator/controllers/tenant_controller.go`
- 数据库Provisioner：`tenant-operator/pkg/database/provisioner.go`
- Tenant CRD定义：`tenant-operator/api/v1/tenant_types.go`

### 设计文档参考
详细的属性定义和验证需求请参考：
`.kiro/specs/multi-tenant-medical-platform/design.md`

属性33的定义：
> **属性 33: Operator自动创建租户数据库**
> *对于任何*新创建的租户，Tenant Operator应该自动在SQL Server上创建该租户的专属数据库实例。
> **验证需求: 7.3**
