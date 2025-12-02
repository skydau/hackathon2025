# 需求文档

## 简介

本文档定义了下一代多租户医疗后台系统的功能需求。该系统旨在为TouchPoint Medical提供一个基于Kubernetes的云原生多租户平台，系统性地解决租户生命周期管理、设备到租户的路由以及数据隔离三大核心挑战。该平台将支持多个医院租户共享基础设施，同时确保每个租户的数据、资源和网络完全隔离，满足医疗行业的安全与合规要求。

## 术语表

- **Tenant（租户）**: 使用平台服务的独立医院客户，每个租户拥有独立的资源、配置和数据隔离
- **Tenant Catalog Service（租户目录服务）**: 存储和管理所有租户元数据的中央权威服务
- **Tenant Operator（租户编排器）**: 基于Kubernetes Operator模式的自动化控制器，负责监听租户资源变化并自动编排相关云资源
- **Device Registry（设备注册表）**: 维护物理设备序列号与租户ID映射关系的服务
- **Smart Gateway（智能网关）**: 作为所有外部流量入口的网关服务，负责设备请求的租户识别和路由
- **DB Router（数据库路由中间件）**: 嵌入在微服务中的中间件，负责根据租户ID动态路由数据库连接
- **X-Tenant-Id**: HTTP请求头字段，用于在系统内部传递租户标识
- **medDispense Station**: 医疗设备终端，用于药品分发等医疗操作
- **CRD (Custom Resource Definition)**: Kubernetes自定义资源定义
- **NetworkPolicy**: Kubernetes网络策略，用于控制Pod之间的网络访问
- **ResourceQuota**: Kubernetes资源配额，用于限制命名空间的资源使用
- **RBAC (Role-Based Access Control)**: 基于角色的访问控制
- **TDE (Transparent Data Encryption)**: 透明数据加密，SQL Server提供的数据库文件加密功能
- **SLO (Service Level Objective)**: 服务等级目标，定义服务质量的可测量指标

## 需求

### 需求 1: 租户注册与元数据管理

**用户故事:** 作为平台管理员，我希望能够注册新的医院租户并管理其元数据，以便系统能够识别和配置每个租户。

#### 验收标准

1. WHEN 管理员通过POST /tenants端点提交包含租户名称和数据库配置的请求 THEN Tenant Catalog Service SHALL 创建新的租户记录并返回唯一的租户ID
2. WHEN 管理员通过GET /tenants/{id}端点请求租户信息 THEN Tenant Catalog Service SHALL 返回该租户的完整元数据，包括名称、状态、数据库连接信息和配置参数
3. WHEN 管理员通过PATCH /tenants/{id}端点更新租户状态 THEN Tenant Catalog Service SHALL 更新租户状态字段并返回更新后的租户信息
4. WHEN 租户被创建 THEN Tenant Catalog Service SHALL 将租户状态初始化为"Provisioning"
5. WHEN 查询不存在的租户ID THEN Tenant Catalog Service SHALL 返回404错误响应

### 需求 2: 租户资源自动化编排

**用户故事:** 作为平台管理员，我希望租户的Kubernetes资源能够自动创建和配置，以便减少手动操作并确保配置一致性。

#### 验收标准

1. WHEN 新的Tenant CRD资源被创建在Kubernetes集群中 THEN Tenant Operator SHALL 自动创建对应的命名空间
2. WHEN Tenant Operator创建命名空间后 THEN Tenant Operator SHALL 在该命名空间内创建包含租户配置的ConfigMap
3. WHEN Tenant Operator创建命名空间后 THEN Tenant Operator SHALL 应用ResourceQuota以限制该租户的CPU和内存使用
4. WHEN Tenant Operator创建命名空间后 THEN Tenant Operator SHALL 应用NetworkPolicy以默认禁止跨命名空间的网络访问
5. WHEN Tenant Operator创建命名空间后 THEN Tenant Operator SHALL 配置RBAC规则以限制对该命名空间的访问权限

### 需求 3: 设备注册与租户映射

**用户故事:** 作为平台管理员，我希望能够注册医疗设备并将其关联到特定租户，以便系统能够正确路由设备请求。

#### 验收标准

1. WHEN 管理员提交包含设备序列号和租户ID的注册请求 THEN Device Registry SHALL 创建设备到租户的映射记录
2. WHEN 系统通过设备序列号查询Device Registry THEN Device Registry SHALL 返回该设备关联的租户ID
3. WHEN 查询未注册的设备序列号 THEN Device Registry SHALL 返回错误响应指示设备未找到
4. WHEN 管理员更新设备的租户关联 THEN Device Registry SHALL 更新映射记录并返回成功响应
5. WHEN 尝试注册已存在的设备序列号 THEN Device Registry SHALL 返回冲突错误响应

### 需求 4: 智能网关请求路由

**用户故事:** 作为系统架构师，我希望智能网关能够识别设备请求并注入租户标识，以便后端服务能够处理多租户请求。

#### 验收标准

1. WHEN Smart Gateway接收到包含设备标识的入站请求 THEN Smart Gateway SHALL 从请求中提取设备序列号
2. WHEN Smart Gateway提取到设备序列号后 THEN Smart Gateway SHALL 调用Device Registry查询对应的租户ID
3. WHEN Smart Gateway获取到租户ID后 THEN Smart Gateway SHALL 将租户ID作为X-Tenant-Id头注入到请求中
4. WHEN Smart Gateway注入X-Tenant-Id头后 THEN Smart Gateway SHALL 将请求转发到内部微服务
5. WHEN Device Registry返回设备未找到错误 THEN Smart Gateway SHALL 拒绝请求并返回401未授权错误

### 需求 5: 租户级流量控制

**用户故事:** 作为平台运维人员，我希望能够为每个租户设置独立的请求速率限制，以便防止单个租户影响整个平台的稳定性。

#### 验收标准

1. WHEN Smart Gateway处理带有X-Tenant-Id头的请求 THEN Smart Gateway SHALL 基于该租户ID应用对应的速率限制策略
2. WHEN 租户的请求速率超过配置的限制 THEN Smart Gateway SHALL 拒绝超出的请求并返回429 Too Many Requests错误
3. WHEN 租户A的请求被速率限制 THEN Smart Gateway SHALL 继续正常处理其他租户的请求
4. WHEN 租户在Tenant CRD中配置了throttling参数 THEN Smart Gateway SHALL 应用该租户特定的速率限制值
5. WHEN 请求不包含X-Tenant-Id头 THEN Smart Gateway SHALL 应用默认的速率限制策略

### 需求 6: 租户感知数据库路由

**用户故事:** 作为后端开发人员，我希望数据库路由中间件能够自动将请求路由到正确的租户数据库，以便业务代码无需关心多租户逻辑。

#### 验收标准

1. WHEN DB Router处理包含X-Tenant-Id头的请求 THEN DB Router SHALL 从请求上下文中提取租户ID
2. WHEN DB Router提取到租户ID后 THEN DB Router SHALL 调用Tenant Catalog Service获取该租户的数据库配置
3. WHEN DB Router获取到数据库配置后 THEN DB Router SHALL 创建或复用到该租户数据库的连接池
4. WHEN DB Router建立连接池后 THEN DB Router SHALL 通过该连接池执行数据库操作
5. WHEN 请求上下文中缺少租户ID THEN DB Router SHALL 抛出错误并拒绝执行数据库操作

### 需求 7: 数据库隔离策略支持

**用户故事:** 作为系统架构师，我希望系统能够支持多种数据库隔离模式，以便根据业务需求在安全性和成本之间进行权衡。

#### 验收标准

1. WHERE 租户配置为Database-per-Tenant模式 THEN DB Router SHALL 连接到该租户的专属数据库实例
2. WHERE 租户配置为Schema-per-Tenant模式 THEN DB Router SHALL 连接到共享数据库实例并使用租户特定的模式
3. WHEN 租户的数据库模式在Tenant CRD中被指定 THEN Tenant Operator SHALL 根据模式创建相应的数据库资源
4. WHEN 使用Database-per-Tenant模式 THEN DB Router SHALL 确保每个租户的连接池完全独立
5. WHEN 使用Schema-per-Tenant模式 THEN DB Router SHALL 在SQL查询中自动添加模式前缀

### 需求 8: 网络层隔离

**用户故事:** 作为安全工程师，我希望每个租户的网络流量被严格隔离，以便防止租户之间的横向攻击。

#### 验收标准

1. WHEN Tenant Operator创建租户命名空间 THEN Tenant Operator SHALL 应用NetworkPolicy默认拒绝所有入站和跨命名空间流量
2. WHEN Pod尝试从一个租户命名空间访问另一个租户命名空间的服务 THEN Kubernetes SHALL 根据NetworkPolicy拒绝该连接
3. WHEN 租户命名空间内的Pod需要访问共享服务 THEN NetworkPolicy SHALL 明确允许到共享服务命名空间的出站流量
4. WHEN NetworkPolicy被应用 THEN Kubernetes SHALL 确保同一租户命名空间内的Pod可以相互通信
5. WHEN 管理员更新租户的NetworkPolicy THEN Kubernetes SHALL 立即应用新的网络规则

### 需求 9: 密钥管理与无凭据访问

**用户故事:** 作为安全工程师，我希望系统使用Azure Key Vault管理敏感凭据，以便消除配置文件中的明文密码。

#### 验收标准

1. WHEN 租户被创建 THEN Tenant Operator SHALL 在Azure Key Vault中创建该租户的数据库凭据密钥
2. WHEN 微服务Pod启动 THEN Secrets Store CSI Driver SHALL 将租户的密钥从Azure Key Vault挂载为Pod内的文件
3. WHEN 密钥在Azure Key Vault中被轮换 THEN Secrets Store CSI Driver SHALL 自动更新Pod内挂载的密钥文件
4. WHEN 微服务需要访问数据库 THEN 微服务 SHALL 使用AKS Workload Identity进行无凭据身份验证
5. WHEN 租户被删除 THEN Tenant Operator SHALL 吊销并删除该租户在Azure Key Vault中的所有密钥

### 需求 10: 数据加密

**用户故事:** 作为合规官，我希望租户数据在静态和传输过程中都被加密，以便满足医疗行业的数据保护要求。

#### 验收标准

1. WHEN 租户数据库被创建 THEN 系统 SHALL 启用SQL Server透明数据加密(TDE)
2. WHEN 数据被写入租户数据库 THEN SQL Server SHALL 自动加密数据库文件和日志
3. WHERE 数据字段被标记为高敏感度 THEN 系统 SHALL 使用Always Encrypted在客户端加密数据
4. WHEN 使用Always Encrypted加密的数据被查询 THEN 只有持有正确密钥的客户端 SHALL 能够解密数据
5. WHEN 数据库备份被创建 THEN SQL Server SHALL 确保备份文件也被加密

### 需求 11: 租户级可观测性

**用户故事:** 作为平台运维人员，我希望能够按租户维度监控系统指标和日志，以便快速定位和解决特定租户的问题。

#### 验收标准

1. WHEN 系统发出Prometheus指标 THEN 所有指标 SHALL 包含tenant_id标签
2. WHEN 系统写入日志 THEN 所有日志条目 SHALL 包含tenant_id字段
3. WHEN 运维人员在Grafana中查询指标 THEN Grafana SHALL 支持按tenant_id过滤和聚合数据
4. WHEN 日志被发送到Loki THEN 系统 SHALL 在请求头中包含X-Scope-OrgID以实现租户级日志隔离
5. WHEN 运维人员查询特定租户的日志 THEN Loki SHALL 只返回该租户的日志条目

### 需求 12: 服务等级目标(SLO)监控

**用户故事:** 作为SRE工程师，我希望能够为每个租户定义和监控SLO，以便主动发现和解决服务质量问题。

#### 验收标准

1. WHEN 租户在Tenant CRD中定义SLO参数 THEN 系统 SHALL 存储该租户的可用性和延迟目标
2. WHEN 系统收集租户的性能指标 THEN 系统 SHALL 计算该租户的实际SLO达成率
3. WHEN 租户的错误率超过SLO定义的阈值 THEN Prometheus SHALL 触发告警通知
4. WHEN 租户的p95延迟超过SLO定义的阈值 THEN Prometheus SHALL 触发告警通知
5. WHEN SLO告警被触发 THEN 告警消息 SHALL 包含租户ID和具体的SLO违规详情

### 需求 13: 租户退服与资源清理

**用户故事:** 作为平台管理员，我希望能够安全地下线租户并清理其资源，以便确保数据安全和资源回收。

#### 验收标准

1. WHEN 管理员将租户状态更新为"Decommissioned" THEN Tenant Operator SHALL 开始执行退服流程
2. WHEN 退服流程开始 THEN Tenant Operator SHALL 首先创建租户数据库的完整备份
3. WHEN 数据备份完成后 THEN Tenant Operator SHALL 吊销该租户在Azure Key Vault中的所有密钥
4. WHEN 密钥被吊销后 THEN Tenant Operator SHALL 删除租户的Kubernetes命名空间及其所有资源
5. WHEN 退服流程完成 THEN Tenant Operator SHALL 保留审计日志记录退服操作的详细信息

### 需求 14: 策略即代码合规检查

**用户故事:** 作为安全工程师，我希望通过OPA Gatekeeper强制执行安全策略，以便在部署前预防配置错误。

#### 验收标准

1. WHEN 开发人员尝试部署工作负载到租户命名空间 THEN OPA Gatekeeper SHALL 验证该工作负载的元数据包含tenantId标签
2. WHEN 工作负载缺少必需的tenantId标签 THEN OPA Gatekeeper SHALL 拒绝部署请求并返回明确的错误消息
3. WHEN 开发人员尝试创建跨租户的NetworkPolicy THEN OPA Gatekeeper SHALL 拒绝该策略并返回违规说明
4. WHEN 策略规则在OPA Gatekeeper中被更新 THEN Gatekeeper SHALL 立即应用新规则到后续的部署请求
5. WHEN 部署请求符合所有策略要求 THEN OPA Gatekeeper SHALL 允许请求通过并继续部署流程

### 需求 15: 成本分摊与资源计量

**用户故事:** 作为财务分析师，我希望能够计算每个租户的资源使用成本，以便进行精细化的成本核算和定价决策。

#### 验收标准

1. WHEN 系统收集租户的资源使用指标 THEN 系统 SHALL 记录该租户的CPU使用量、内存占用、存储用量和网络流量
2. WHEN 计算租户成本时 THEN 系统 SHALL 将资源使用量乘以预设的费率得出估算成本
3. WHEN 生成月度成本报告 THEN 系统 SHALL 为每个租户生成包含各项资源成本明细的报告
4. WHEN 租户的资源使用超过ResourceQuota限制 THEN Kubernetes SHALL 阻止该租户继续分配资源
5. WHEN 查询租户的历史成本数据 THEN 系统 SHALL 返回指定时间范围内的成本趋势数据
