# MedLogic 多租户平台完整部署指南

本指南将帮你完整部署 MedLogic 多租户医疗平台，包括所有服务和模拟器。

## 🏗️ 系统架构

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│  MedDispense    │───▶│   Smart Gateway  │───▶│  MedLogic       │
│  Stations       │    │   (NGINX + Lua)  │    │  Service        │
│  (模拟器)       │    │                  │    │                 │
└─────────────────┘    └──────────┬───────┘    └─────────┬───────┘
      Device-Id                   │                      │ X-Tenant-Id
                       ┌──────────▼───────┐              │
                       │ Device Registry  │              │
                       │ (设备→租户映射)   │              │
                       └──────────────────┘              │
                                                         │
                       ┌─────────────────┐              │
                       │ TenantDBRouter  │◀─────────────┘
                       │                 │
                       └─────────┬───────┘
                                 │
                    ┌────────────▼────────────┐
                    │     租户数据库           │
                    │  hospital-001_DB       │
                    │  clinic-002_DB         │
                    └────────────────────────┘
```

## 📋 部署步骤

### 1. 启动基础服务

#### 方式一：Docker Compose（推荐）
```bash
# 启动完整系统
docker-compose -f docker-compose.full-system.yml up --build -d

# 等待所有服务启动
docker-compose -f docker-compose.full-system.yml ps
```

#### 方式二：Kubernetes
```bash
# 启动 Minikube
minikube start

# 部署所有服务
kubectl apply -f k8s/local/
```

### 2. 验证服务状态

```bash
# 检查服务健康状态
curl http://localhost:8080/health    # Tenant Catalog
curl http://localhost:8081/health    # Device Registry  
curl http://localhost:5000/health    # MedLogic Service
curl http://localhost:30000/health   # Smart Gateway
```

### 3. 创建租户

```bash
# 创建第一个租户（医院）
curl -X POST http://localhost:8080/api/tenants \
  -H "Content-Type: application/json" \
  -d '{
    "displayName": "第一医院",
    "dbConfig": {
      "mode": "perDatabase",
      "server": "sqlserver",
      "database": "Hospital001_DB",
      "credentialRef": "keyvault/hospital001-password"
    },
    "throttling": {
      "rps": 100
    }
  }'

# 创建第二个租户（诊所）
curl -X POST http://localhost:8080/api/tenants \
  -H "Content-Type: application/json" \
  -d '{
    "displayName": "社区诊所",
    "dbConfig": {
      "mode": "perDatabase", 
      "server": "sqlserver",
      "database": "Clinic002_DB",
      "credentialRef": "keyvault/clinic002-password"
    },
    "throttling": {
      "rps": 50
    }
  }'
```

### 4. 注册设备

```bash
# Windows
.\scripts\register-devices.ps1

# Linux/Mac
chmod +x scripts/register-devices.sh
./scripts/register-devices.sh
```

### 5. 启动模拟器

```bash
# Windows
.\scripts\run-simulator.ps1

# Linux/Mac  
./scripts/run-simulator.sh
```

### 6. 验证数据流

```bash
# Windows
.\scripts\test-simulator.ps1

# Linux/Mac
# 手动测试
curl -H "Device-Id: MED-STATION-001" http://localhost:30000/api/transactions
```

## 🔍 验证和监控

### 查看事务数据

```bash
# 通过 Smart Gateway 查看租户事务
curl -H "Device-Id: MED-STATION-001" http://localhost:30000/api/transactions
curl -H "Device-Id: MED-STATION-003" http://localhost:30000/api/transactions

# 直接查看 MedLogic Service（需要租户ID）
curl -H "X-Tenant-Id: hospital-001" http://localhost:5000/api/transactions
curl -H "X-Tenant-Id: clinic-002" http://localhost:5000/api/transactions
```

### 查看设备注册

```bash
# 查看所有设备
curl http://localhost:8081/api/devices

# 查看特定设备
curl http://localhost:8081/api/devices/MED-STATION-001

# 查看设备租户映射
curl http://localhost:8081/api/devices/MED-STATION-001/tenant
```

### 查看租户信息

```bash
# 查看所有租户
curl http://localhost:8080/api/tenants

# 查看特定租户
curl http://localhost:8080/api/tenants/hospital-001
```

## 📊 监控日志

### Docker 环境

```bash
# 查看所有服务日志
docker-compose -f docker-compose.full-system.yml logs -f

# 查看特定服务日志
docker-compose -f docker-compose.full-system.yml logs -f smart-gateway
docker-compose -f docker-compose.full-system.yml logs -f med-dispense-simulator
docker-compose -f docker-compose.full-system.yml logs -f medlogic-service
```

### Kubernetes 环境

```bash
# 查看 Smart Gateway 日志
kubectl logs -n gateway deployment/smart-gateway -f

# 查看模拟器日志（如果部署在 K8s 中）
kubectl logs -l app=med-dispense-simulator -f
```

## 🧪 测试场景

### 1. 基本功能测试

```bash
# 发送单个事务
curl -X POST http://localhost:30000/api/transactions \
  -H "Device-Id: MED-STATION-001" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 89.50,
    "description": "阿莫西林胶囊 + 维生素C片"
  }'
```

### 2. 多租户隔离测试

```bash
# 医院设备发送事务
curl -X POST http://localhost:30000/api/transactions \
  -H "Device-Id: MED-STATION-001" \
  -H "Content-Type: application/json" \
  -d '{"amount": 100.00, "description": "医院事务"}'

# 诊所设备发送事务  
curl -X POST http://localhost:30000/api/transactions \
  -H "Device-Id: MED-STATION-003" \
  -H "Content-Type: application/json" \
  -d '{"amount": 50.00, "description": "诊所事务"}'

# 验证数据隔离
curl -H "X-Tenant-Id: hospital-001" http://localhost:5000/api/transactions
curl -H "X-Tenant-Id: clinic-002" http://localhost:5000/api/transactions
```

### 3. 错误处理测试

```bash
# 未注册设备
curl -X POST http://localhost:30000/api/transactions \
  -H "Device-Id: UNKNOWN-DEVICE" \
  -H "Content-Type: application/json" \
  -d '{"amount": 100.00, "description": "测试"}'

# 缺少设备ID
curl -X POST http://localhost:30000/api/transactions \
  -H "Content-Type: application/json" \
  -d '{"amount": 100.00, "description": "测试"}'
```

## 🛠️ 故障排除

### 常见问题

1. **Smart Gateway 返回 400 错误**
   - 检查是否包含 `Device-Id` 头部
   - 验证设备是否已注册

2. **设备未找到 (401 错误)**
   - 运行设备注册脚本
   - 检查 Device Registry 服务状态

3. **服务连接失败**
   - 检查所有服务是否启动
   - 验证网络连接和端口

4. **数据库连接失败**
   - 检查 SQL Server 是否运行
   - 验证连接字符串配置

### 调试命令

```bash
# 检查服务状态
docker-compose -f docker-compose.full-system.yml ps

# 重启特定服务
docker-compose -f docker-compose.full-system.yml restart smart-gateway

# 查看详细错误
docker-compose -f docker-compose.full-system.yml logs smart-gateway --tail=50
```

## 🎯 性能测试

### 压力测试

```bash
# 使用 Apache Bench 进行压力测试
ab -n 1000 -c 10 -H "Device-Id: MED-STATION-001" \
   -p test-transaction.json -T application/json \
   http://localhost:30000/api/transactions
```

### 并发测试

```bash
# 多设备并发发送
for device in MED-STATION-001 MED-STATION-002 MED-STATION-003 MED-STATION-004; do
  curl -X POST http://localhost:30000/api/transactions \
    -H "Device-Id: $device" \
    -H "Content-Type: application/json" \
    -d '{"amount": 75.00, "description": "并发测试"}' &
done
wait
```

## 📈 扩展和定制

### 添加新租户

1. 通过 API 创建租户
2. Tenant Operator 自动创建数据库
3. 注册该租户的设备
4. 更新模拟器配置

### 添加新设备类型

1. 在 Device Registry 中注册
2. 更新模拟器配置
3. 自定义消息格式

### 自定义业务逻辑

1. 修改 MedLogicService 控制器
2. 添加新的 API 端点
3. 更新数据库模式

## 🔒 生产部署注意事项

1. **安全配置**
   - 使用强密码和证书
   - 启用 HTTPS
   - 配置防火墙规则

2. **监控和日志**
   - 集成 Prometheus/Grafana
   - 配置日志聚合
   - 设置告警规则

3. **高可用性**
   - 多副本部署
   - 负载均衡配置
   - 数据库集群

4. **备份和恢复**
   - 定期数据库备份
   - 灾难恢复计划
   - 配置版本控制