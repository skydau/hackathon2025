# 实施计划

## 当前状态总结

已完成的核心功能：
- ✅ 项目基础架构（.NET解决方案、Docker配置）
- ✅ Tenant Catalog Service（租户CRUD、数据库配置管理）
- ✅ Device Registry Service（设备注册、租户映射）
- ✅ DB Router中间件（租户感知数据库路由）
- ✅ Tenant Operator基础功能（命名空间、ConfigMap、ResourceQuota、NetworkPolicy、RBAC）
- ✅ Smart Gateway（NGINX + Lua，设备识别、租户ID注入、速率限制）
- ✅ 密钥管理（Kubernetes Secret集成）
- ✅ 数据加密（TDE、Always Encrypted）
- ✅ 网络隔离（NetworkPolicy）
- ✅ OPA Gatekeeper策略
- ✅ 可观测性（Prometheus、Loki、Grafana集成）
- ✅ SLO监控和告警
- ✅ 租户退服流程
- ✅ 成本分摊功能
- ✅ 示例后端微服务（MedLogicService）
- ✅ 演示UI（AdminUI）
- ✅ 大部分属性测试和集成测试

待完成的关键功能：
- ⏳ **数据库自动创建功能**（任务6.6-6.14）：Tenant Operator需要实现自动连接本地SQL Server为每个租户创建独立数据库
- ⏳ **本地部署配置**（任务18-18.3）：需要创建完整的Kubernetes部署清单、启动脚本和配置文件
- ⏳ **端到端测试**（任务18.3）：验证从租户创建到数据库自动配置的完整流程

## 下一步行动

优先完成以下任务以实现完整的本地部署：
1. 任务6.6-6.14：实现数据库自动创建功能
2. 任务18-18.3：配置本地开发环境和部署清单
3. 任务20：最终检查点，确保所有测试通过

---

- [x] 1. 搭建项目基础架构





  - 创建.NET解决方案结构，包含Tenant Catalog Service、Device Registry Service和共享库项目
  - 配置Entity Framework Core和数据库迁移
  - 设置Docker容器化配置和Kubernetes部署清单
  - 配置日志记录（Serilog）和健康检查端点
  - _需求: 1.1, 3.1_

- [x] 1.1 编写项目基础架构的单元测试


  - 测试EF Core DbContext配置
  - 测试健康检查端点
  - _需求: 1.1, 3.1_

- [x] 2. 实现Tenant Catalog Service核心功能





  - 实现Tenant实体模型和DbContext
  - 实现租户CRUD操作的Repository层
  - 实现租户管理的REST API端点（POST, GET, PATCH）
  - 实现租户状态管理逻辑
  - _需求: 1.1, 1.2, 1.3, 1.4_

- [x] 2.1 编写属性测试：租户创建返回唯一ID


  - **属性 1: 租户创建返回唯一ID**
  - **验证需求: 1.1**

- [x] 2.2 编写属性测试：租户数据往返一致性


  - **属性 2: 租户数据往返一致性**
  - **验证需求: 1.2**

- [x] 2.3 编写属性测试：租户状态更新反映正确

  - **属性 3: 租户状态更新反映正确**
  - **验证需求: 1.3**

- [x] 2.4 编写属性测试：新租户初始状态一致

  - **属性 4: 新租户初始状态一致**
  - **验证需求: 1.4**

- [x] 2.5 编写属性测试：不存在的租户返回404

  - **属性 5: 不存在的租户返回404**
  - **验证需求: 1.5**

- [x] 3. 实现Device Registry Service





  - 实现Device实体模型和DbContext
  - 实现设备注册和查询的Repository层
  - 实现设备管理的REST API端点
  - 实现设备到租户映射查询端点（供Smart Gateway调用）
  - _需求: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 3.1 编写属性测试：设备注册创建映射


  - **属性 11: 设备注册创建映射**
  - **验证需求: 3.1**



- [x] 3.2 编写属性测试：设备查询往返一致性





  - **属性 12: 设备查询往返一致性**


  - **验证需求: 3.2**



- [x] 3.3 编写属性测试：未注册设备返回错误





  - **属性 13: 未注册设备返回错误**


  - **验证需求: 3.3**

- [x] 3.4 编写属性测试：设备租户关联可更新





  - **属性 14: 设备租户关联可更新**
  - **验证需求: 3.4**

- [x] 3.5 编写属性测试：重复注册返回冲突





  - **属性 15: 重复注册返回冲突**
  - **验证需求: 3.5**

- [x] 4. 实现DB Router中间件







  - 创建TenantDBRouter NuGet包项目
  - 实现租户级数据库连接管理（每个租户独立数据库）
  - 实现从X-Tenant-Id头提取租户ID的中间件
  - 实现调用Tenant Catalog获取数据库配置的客户端
  - 实现从Kubernetes Secret读取数据库密码
  - _需求: 6.1, 6.2, 6.3, 6.4, 6.5, 7.1, 7.2, 7.3_

- [x] 4.1 编写属性测试：DB Router提取租户ID


  - **属性 26: DB Router提取租户ID**
  - **验证需求: 6.1**


- [x] 4.2 编写属性测试：DB Router查询数据库配置

  - **属性 27: DB Router查询数据库配置**
  - **验证需求: 6.2**



- [x] 4.3 编写属性测试：连接池复用

  - **属性 28: 连接池复用**

  - **验证需求: 6.3**

- [x] 4.4 编写属性测试：通过正确连接池执行操作

  - **属性 29: 通过正确连接池执行操作**
  - **验证需求: 6.4**


- [x] 4.5 编写属性测试：缺少租户ID抛出错误

  - **属性 30: 缺少租户ID抛出错误**
  - **验证需求: 6.5**


- [x] 4.6 编写属性测试：每个租户连接专属数据库

  - **属性 31: 每个租户连接专属数据库**
  - **验证需求: 7.1**

- [x] 4.7 编写属性测试：租户连接池独立

  - **属性 32: 租户连接池独立**
  - **验证需求: 7.2**

- [x] 5. 配置Smart Gateway (NGINX)





  - 编写NGINX配置文件，实现设备ID提取
  - 实现Lua脚本调用Device Registry查询租户ID
  - 实现X-Tenant-Id头注入逻辑
  - 配置租户级速率限制（基于X-Tenant-Id）
  - 配置请求转发到后端服务
  - _需求: 4.1, 4.2, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3, 5.4, 5.5_

- [x] 5.1 编写集成测试：网关端到端请求流


  - 测试从设备请求到租户ID注入的完整流程
  - _需求: 4.1, 4.2, 4.3, 4.4_


- [x] 5.2 编写属性测试：租户级速率限制应用

  - **属性 21: 租户级速率限制应用**
  - **验证需求: 5.1**

- [x] 5.3 编写属性测试：超速请求返回429

  - **属性 22: 超速请求返回429**
  - **验证需求: 5.2**

- [x] 5.4 编写属性测试：租户隔离不受影响

  - **属性 23: 租户隔离不受影响**
  - **验证需求: 5.3**

- [x] 6. 开发Tenant Operator (Go)





  - 使用kubebuilder初始化Operator项目
  - 定义Tenant CRD (tenants.medlogic.io/v1)，移除数据库模式选项
  - 实现Reconcile循环处理Tenant资源创建
  - 实现命名空间自动创建逻辑
  - 实现ConfigMap和Secret创建
  - 实现ResourceQuota应用
  - 实现NetworkPolicy应用
  - 实现RBAC规则配置
  - _需求: 2.1, 2.2, 2.3, 2.4, 2.5, 7.3_

- [x] 6.1 编写属性测试：CRD创建触发命名空间创建


  - **属性 6: CRD创建触发命名空间创建**
  - **验证需求: 2.1**


- [x] 6.2 编写属性测试：命名空间包含配置ConfigMap





  - **属性 7: 命名空间包含配置ConfigMap**

  - **验证需求: 2.2**

- [x] 6.3 编写属性测试：命名空间应用资源配额

  - **属性 8: 命名空间应用资源配额**
  - **验证需求: 2.3**


- [x] 6.4 编写属性测试：命名空间应用网络策略





  - **属性 9: 命名空间应用网络策略**
  - **验证需求: 2.4**

- [x] 6.5 编写属性测试：命名空间配置RBAC





  - **属性 10: 命名空间配置RBAC**
  - **验证需求: 2.5**

- [x] 6.6 实现数据库自动创建功能（本地SQL Server）





  - 在tenant-operator/pkg目录下创建database包
  - 实现DatabaseProvisioner结构体和ProvisionDatabase方法
  - 实现通过host.minikube.internal连接本地SQL Server（使用github.com/denisenkom/go-mssqldb驱动）
  - 为每个租户创建独立的数据库实例（使用SQL Server管理员凭据）
  - 实现数据库用户创建和权限配置（CREATE LOGIN, CREATE USER, ALTER ROLE）
  - 实现TDE加密自动启用（可选，需要预先配置服务器证书）
  - 实现数据库初始化脚本执行（从ConfigMap或嵌入的SQL文件读取）
  - 实现将数据库凭据存储到Kubernetes Secret（在租户命名空间中）
  - 实现调用Tenant Catalog Service注册数据库配置
  - 实现错误处理和回滚机制（删除部分创建的数据库、清理Secret）
  - 在TenantReconciler中集成DatabaseProvisioner，在命名空间创建后调用
  - 更新Tenant CRD status字段（databaseCreated, secretCreated）
  - _需求: 2.6, 2.7, 2.8, 2.9_

- [x] 6.7 编写属性测试：Operator自动创建租户数据库





  - **属性 33: Operator自动创建租户数据库**
  - **验证需求: 7.3**
  - 在tenant-operator/controllers/tenant_controller_test.go中添加测试
  - 使用gopter生成随机Tenant实例
  - 验证数据库在SQL Server上被创建

- [x] 6.8 编写属性测试：数据库初始化脚本执行





  - **属性 10.2: 数据库初始化脚本执行**
  - **验证需求: 2.7**
  - 验证初始化脚本中定义的表在数据库中存在

- [x] 6.9 编写属性测试：数据库创建失败状态更新





  - **属性 10.3: 数据库创建失败状态更新**
  - **验证需求: 2.8**
  - 模拟数据库创建失败场景
  - 验证Tenant CRD的status.phase被更新为"Failed"

- [x] 6.10 编写属性测试：数据库配置注册到Catalog





  - **属性 10.4: 数据库配置注册到Catalog**
  - **验证需求: 2.9**
  - 验证数据库配置被正确注册到Tenant Catalog Service

- [x] 6.11 创建租户数据库初始化脚本





  - 在scripts/sql目录下创建init-tenant-db.sql
  - 定义Transactions表（Id, Amount, Description, CreatedAt）
  - 定义AuditLogs表（Id, Action, UserId, Timestamp, Details）
  - 创建必要的索引（IX_Transactions_CreatedAt, IX_AuditLogs_Timestamp）
  - 在Tenant Operator中嵌入SQL脚本或从ConfigMap读取
  - _需求: 2.7_

- [x] 6.12 实现Tenant Catalog客户端（Go）





  - 在tenant-operator/pkg目录下创建catalog包
  - 实现TenantCatalogClient结构体
  - 实现UpdateTenantDbConfig方法（调用Tenant Catalog的PATCH /api/tenants/{id}端点）
  - 实现HTTP客户端配置（超时、重试机制）
  - 在TenantReconciler中集成TenantCatalogClient
  - _需求: 2.9_

- [x] 6.13 集成数据库自动创建到Reconcile循环





  - 在TenantReconciler结构体中添加DatabaseProvisioner和CatalogClient字段
  - 在Reconcile方法中，在创建Secret之后调用DatabaseProvisioner.ProvisionDatabase
  - 更新Tenant CRD status.databaseCreated字段
  - 处理数据库创建失败的情况（更新status.phase为"Failed"，记录错误到status.conditions）
  - 实现幂等性（检查数据库是否已存在，避免重复创建）
  - 在main.go中初始化DatabaseProvisioner（从环境变量读取SQL Server连接信息）
  - _需求: 2.6, 2.8_

- [x] 6.14 检查点 - 确保数据库自动创建功能测试通过





  - 确保所有数据库自动创建相关的属性测试通过
  - 如有问题，请向用户报告

- [x] 7. 集成Kubernetes Secret密钥管理





  - 在Tenant Operator中实现Kubernetes Secret创建逻辑
  - 实现租户创建时生成数据库凭据并存储到Secret
  - 配置微服务Pod挂载Secret（环境变量或文件）
  - 实现DB Router从Secret读取数据库密码
  - 实现租户删除时删除相关Secret
  - _需求: 9.1, 9.2, 9.5_

- [x] 7.1 编写属性测试：租户创建时创建Secret


  - **属性 41: 租户创建时创建Secret**
  - **验证需求: 9.1**

- [x] 7.2 编写集成测试：Secret更新后Pod重启


  - 测试Secret更新后Pod能够获取新凭据
  - _需求: 9.3_

- [x] 7.3 编写属性测试：租户删除时删除Secret


  - **属性 45: 租户删除时删除Secret**
  - **验证需求: 9.5**

- [x] 8. 配置SQL Server认证





  - 配置微服务使用SQL Server用户名/密码认证
  - 实现从Kubernetes Secret读取数据库凭据
  - 配置连接字符串使用host.minikube.internal访问宿主机SQL Server
  - 实现连接字符串加密和安全存储
  - 测试微服务与本地SQL Server的连接
  - _需求: 9.4_

- [x] 8.1 编写集成测试：SQL Server认证访问


  - 验证微服务可以使用Secret中的凭据访问数据库
  - _需求: 9.4_

- [x] 9. 实现数据加密





  - 在租户数据库创建脚本中启用TDE
  - 为敏感字段配置Always Encrypted
  - 实现客户端加密密钥管理
  - 配置数据库备份加密
  - _需求: 10.1, 10.3, 10.4, 10.5_

- [x] 9.1 编写属性测试：租户数据库启用TDE


  - **属性 46: 租户数据库启用TDE**
  - **验证需求: 10.1**


- [x] 9.2 编写属性测试：敏感字段使用Always Encrypted

  - **属性 47: 敏感字段使用Always Encrypted**
  - **验证需求: 10.3**



- [x] 9.3 编写属性测试：加密数据访问控制





  - **属性 48: 加密数据访问控制**
  - **验证需求: 10.4**

- [x] 10. 实现网络隔离





  - 编写NetworkPolicy模板（默认拒绝跨命名空间流量）
  - 在Tenant Operator中应用NetworkPolicy
  - 配置允许访问共享服务的出站规则
  - 配置允许命名空间内Pod通信的规则
  - _需求: 8.1, 8.2, 8.3, 8.4_

- [x] 10.1 编写集成测试：跨租户访问被拒绝


  - **属性 37: 跨租户访问被拒绝**
  - **验证需求: 8.2**

- [x] 10.2 编写属性测试：共享服务访问允许


  - **属性 38: 共享服务访问允许**
  - **验证需求: 8.3**

- [x] 10.3 编写属性测试：命名空间内通信允许

  - **属性 39: 命名空间内通信允许**
  - **验证需求: 8.4**

- [x] 11. 配置OPA Gatekeeper策略





  - 安装OPA Gatekeeper到Kubernetes集群
  - 编写ConstraintTemplate验证tenantId标签
  - 编写Constraint强制所有工作负载包含tenantId标签
  - 编写策略拒绝跨租户NetworkPolicy
  - 测试策略执行和拒绝场景
  - _需求: 14.1, 14.2, 14.3, 14.4, 14.5_

- [x] 11.1 编写属性测试：Gatekeeper验证租户标签


  - **属性 65: Gatekeeper验证租户标签**
  - **验证需求: 14.1**


- [x] 11.2 编写属性测试：缺少标签拒绝部署





  - **属性 66: 缺少标签拒绝部署**
  - **验证需求: 14.2**


- [x] 11.3 编写属性测试：跨租户策略被拒绝





  - **属性 67: 跨租户策略被拒绝**
  - **验证需求: 14.3**

- [x] 12. 实现租户级可观测性





  - 配置Prometheus指标收集，所有指标添加tenant_id标签
  - 配置Loki日志聚合，所有日志添加tenant_id字段
  - 配置日志发送时包含X-Scope-OrgID头
  - 创建Grafana仪表盘，支持按tenant_id过滤
  - 实现按租户的指标和日志查询
  - _需求: 11.1, 11.2, 11.3, 11.4, 11.5_

- [x] 12.1 编写属性测试：指标包含租户标签


  - **属性 50: 指标包含租户标签**
  - **验证需求: 11.1**

- [x] 12.2 编写属性测试：日志包含租户字段


  - **属性 51: 日志包含租户字段**
  - **验证需求: 11.2**

- [x] 12.3 编写属性测试：日志请求包含租户头


  - **属性 53: 日志请求包含租户头**
  - **验证需求: 11.4**

- [x] 12.4 编写属性测试：日志查询租户隔离


  - **属性 54: 日志查询租户隔离**
  - **验证需求: 11.5**

- [x] 13. 实现SLO监控和告警





  - 在Tenant CRD中添加SLO配置字段
  - 实现SLO配置存储到Tenant Catalog
  - 配置Prometheus计算SLO达成率
  - 配置错误率超过阈值的告警规则
  - 配置p95延迟超过阈值的告警规则
  - 配置告警消息包含租户ID和违规详情
  - _需求: 12.1, 12.2, 12.3, 12.4, 12.5_

- [x] 13.1 编写属性测试：SLO配置存储


  - **属性 55: SLO配置存储**
  - **验证需求: 12.1**

- [x] 13.2 编写属性测试：SLO达成率计算

  - **属性 56: SLO达成率计算**
  - **验证需求: 12.2**

- [x] 13.3 编写集成测试：错误率告警触发


  - 模拟高错误率场景，验证告警触发
  - _需求: 12.3_


- [x] 13.4 编写集成测试：延迟告警触发





  - 模拟高延迟场景，验证告警触发
  - _需求: 12.4_

- [x] 14. 实现租户退服流程





  - 在Tenant Operator中实现退服状态检测
  - 实现数据库备份创建逻辑
  - 实现密钥吊销逻辑
  - 实现命名空间和资源删除逻辑
  - 实现审计日志记录
  - _需求: 13.1, 13.2, 13.3, 13.4, 13.5_

- [x] 14.1 编写属性测试：退服状态触发流程


  - **属性 60: 退服状态触发流程**
  - **验证需求: 13.1**

- [x] 14.2 编写属性测试：退服首先创建备份

  - **属性 61: 退服首先创建备份**
  - **验证需求: 13.2**

- [x] 14.3 编写属性测试：备份后删除Secret

  - **属性 62: 备份后删除Secret**
  - **验证需求: 13.3**

- [x] 14.4 编写属性测试：Secret删除后删除命名空间

  - **属性 63: Secret删除后删除命名空间**
  - **验证需求: 13.4**

- [x] 14.5 编写属性测试：退服保留审计日志

  - **属性 64: 退服保留审计日志**
  - **验证需求: 13.5**

- [x] 15. 实现成本分摊功能





  - 配置Prometheus收集租户资源使用指标（CPU、内存、存储、网络）
  - 实现成本计算服务，根据资源使用量和费率计算成本
  - 实现月度成本报告生成
  - 实现历史成本数据查询API
  - 验证ResourceQuota限制强制执行
  - _需求: 15.1, 15.2, 15.3, 15.4, 15.5_

- [x] 15.1 编写属性测试：资源使用指标记录


  - **属性 70: 资源使用指标记录**
  - **验证需求: 15.1**

- [x] 15.2 编写属性测试：成本计算公式正确

  - **属性 71: 成本计算公式正确**
  - **验证需求: 15.2**

- [x] 15.3 编写属性测试：月度报告包含明细

  - **属性 72: 月度报告包含明细**
  - **验证需求: 15.3**

- [x] 15.4 编写属性测试：配额限制强制执行

  - **属性 73: 配额限制强制执行**
  - **验证需求: 15.4**

- [x] 16. 创建示例后端微服务




  - 创建medLogic微服务项目（ASP.NET Core Web API）
  - 集成DB Router中间件
  - 实现示例业务端点（如交易记录）
  - 配置健康检查和日志记录
  - 部署到Kubernetes并验证多租户路由
  - _需求: 6.1, 6.2, 6.3, 6.4_

- [x] 16.1 编写集成测试：端到端多租户请求流


  - 测试从设备请求到数据库写入的完整流程
  - 验证不同租户的数据正确隔离
  - _需求: 4.1, 4.2, 4.3, 4.4, 6.1, 6.2, 6.3, 6.4_

- [x] 17. 创建演示UI





  - 创建简单的Web UI用于租户管理（Blazor或React）
  - 实现租户创建表单
  - 实现租户列表和状态查看
  - 实现设备注册表单
  - 实现基本的监控仪表盘（嵌入Grafana）
  - _需求: 1.1, 1.2, 3.1_

- [x] 18. 配置本地开发环境和部署清单





  - 在k8s/local目录下创建Kubernetes部署清单
  - 创建namespace.yaml（定义platform-system、gateway、observability命名空间）
  - 创建tenant-catalog-deployment.yaml（Deployment和Service）
  - 创建device-registry-deployment.yaml（Deployment和Service）
  - 创建smart-gateway-deployment.yaml（NGINX网关的Deployment和Service）
  - 创建tenant-operator-deployment.yaml（Operator的Deployment和RBAC）
  - 创建sqlserver-admin-secret.yaml（SQL Server管理员凭据Secret模板）
  - 配置所有Deployment使用imagePullPolicy: Never（本地镜像）
  - 配置Service使用NodePort类型便于本地访问
  - 创建Minikube启动脚本（scripts/start-minikube.sh）
  - 创建本地镜像构建脚本（scripts/build-images.sh）
  - 创建一键部署脚本（scripts/deploy-local.sh）
  - 更新docs/LOCAL_ENVIRONMENT_GUIDE.zh.md补充详细的配置步骤
  - 编写本地环境故障排查指南（docs/TROUBLESHOOTING.zh.md）

- [x] 18.1 创建示例Tenant CRD配置


  - 在tenant-operator/config/samples目录下创建完整的示例
  - 创建hospital-a.yaml（示例租户A配置）
  - 创建hospital-b.yaml（示例租户B配置）
  - 包含完整的db、throttling、slo配置
  - 添加注释说明各字段含义

- [x] 18.2 创建SQL Server配置脚本


  - 在scripts/sql目录下创建setup-sqlserver.sql
  - 创建TDE主密钥和证书
  - 创建MedLogicPlatform平台数据库
  - 创建Tenants和Devices表
  - 配置SQL Server认证和权限
  - 添加验证查询

- [x] 18.3 编写端到端集成测试脚本


  - 创建scripts/test-e2e.sh脚本
  - 测试创建Tenant CRD触发数据库自动创建
  - 验证数据库在SQL Server上被创建
  - 验证Secret在租户命名空间中被创建
  - 验证数据库配置被注册到Tenant Catalog
  - 测试通过Smart Gateway发送请求到后端服务
  - 验证数据被写入正确的租户数据库
  - 测试租户退服流程（备份、删除）

- [x] 19. 编写部署文档和脚本





  - 编写Kubernetes部署清单（Deployment、Service、Ingress）
  - 编写本地环境部署清单（使用imagePullPolicy: Never）
  - 编写部署文档，包含前置条件和步骤说明
  - 创建自动化部署脚本（构建镜像、部署到Minikube）
  - 编写故障排查指南

- [x] 20. 最终检查点 - 确保所有测试通过





  - 运行所有单元测试
  - 运行所有属性测试
  - 运行所有集成测试
  - 验证端到端场景
  - 如有问题，请向用户报告
