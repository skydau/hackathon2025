# MedDispense 配送站模拟器使用指南

本模拟器用于模拟医疗配送站设备向 Smart Gateway 发送消息，将业务数据写入租户数据库。

## 🏗️ 系统架构

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│  MedDispense    │───▶│   Smart Gateway  │───▶│  MedLogic       │
│  Stations       │    │   (NGINX + Lua)  │    │  Service        │
│  (模拟器)       │    │                  │    │                 │
└─────────────────┘    └──────────┬───────┘    └─────────┬───────┘
                                  │                      │
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

## 📋 功能特性

- **多租户支持**: 模拟多个租户的配送站设备
- **真实数据**: 生成真实的药品配送数据
- **批量处理**: 支持批量发送事务
- **可配置**: 灵活的配置选项
- **监控日志**: 详细的操作日志

## 🚀 快速开始

### 方式一：直接运行 (.NET)

#### 前提条件
- .NET 9.0 SDK
- MedLogicService 正在运行 (http://localhost:5000)

#### Windows (PowerShell)
```powershell
# 使用默认配置
.\scripts\run-simulator.ps1

# 自定义配置
.\scripts\run-simulator.ps1 -MedLogicUrl "http://localhost:5000" -IntervalSeconds 3 -BatchSize 3
```

#### Linux/Mac (Bash)
```bash
# 给脚本执行权限
chmod +x scripts/run-simulator.sh

# 使用默认配置
./scripts/run-simulator.sh

# 自定义配置
./scripts/run-simulator.sh --url "http://localhost:5000" --interval 3 --batch 3
```

#### 手动运行
```bash
cd tools/MedDispenseSimulator
dotnet build
dotnet run
```

### 方式二：Docker 运行

#### 单独运行模拟器
```bash
# 构建镜像
docker build -f tools/MedDispenseSimulator/Dockerfile -t medlogic-simulator .

# 运行容器
docker run --rm \
  -e MedLogicService__BaseUrl=http://host.docker.internal:5000 \
  -e Simulation__IntervalSeconds=5 \
  -e Simulation__BatchSize=2 \
  --name medlogic-simulator \
  medlogic-simulator
```

#### 完整系统运行
```bash
# 启动完整系统（包括模拟器）
docker-compose -f docker-compose.full-system.yml up --build

# 只启动模拟器相关服务
docker-compose -f docker-compose.full-system.yml up sqlserver tenant-catalog medlogic-service med-dispense-simulator
```

## ⚙️ 配置说明

### appsettings.json 配置

```json
{
  "MedLogicService": {
    "BaseUrl": "http://localhost:5000",  // MedLogicService 地址
    "Timeout": 30                        // 请求超时时间（秒）
  },
  "Simulation": {
    "IntervalSeconds": 5,                // 发送间隔（秒）
    "BatchSize": 3,                      // 批量大小
    "RandomizeAmounts": true,            // 是否随机金额
    "MinAmount": 10.00,                  // 最小金额
    "MaxAmount": 500.00                  // 最大金额
  },
  "Devices": [                           // 设备配置
    {
      "SerialNumber": "MED-STATION-001",
      "TenantId": "hospital-001",
      "Location": "急诊科",
      "Description": "急诊科药品配送站"
    }
  ]
}
```

### 环境变量配置

| 环境变量 | 说明 | 默认值 |
|---------|------|--------|
| `MedLogicService__BaseUrl` | MedLogicService 地址 | http://localhost:5000 |
| `MedLogicService__Timeout` | 请求超时时间 | 30 |
| `Simulation__IntervalSeconds` | 发送间隔 | 5 |
| `Simulation__BatchSize` | 批量大小 | 3 |

## 📊 模拟数据

### 设备配置
- **MED-STATION-001**: 医院急诊科 (hospital-001)
- **MED-STATION-002**: 医院内科病房 (hospital-001)
- **MED-STATION-003**: 诊所门诊大厅 (clinic-002)
- **MED-STATION-004**: 诊所儿科诊室 (clinic-002)

### 药品数据
- 阿莫西林胶囊 (¥15.50)
- 布洛芬片 (¥12.80)
- 维生素C片 (¥8.90)
- 感冒灵颗粒 (¥18.60)
- 头孢克肟胶囊 (¥32.40)
- 等等...

### 生成的事务数据
每个配送事务包含：
- **设备信息**: 序列号、位置
- **患者信息**: 患者ID
- **药品信息**: 药品名称、数量、单价
- **总金额**: 自动计算
- **时间戳**: 配送时间

## 🔍 监控和调试

### 查看日志
```bash
# 实时查看模拟器日志
docker-compose -f docker-compose.full-system.yml logs -f med-dispense-simulator

# 查看 MedLogicService 日志
docker-compose -f docker-compose.full-system.yml logs -f medlogic-service
```

### 验证数据
```bash
# 查看租户事务数据
curl -H "X-Tenant-Id: hospital-001" http://localhost:5000/api/transactions

# 查看特定租户的事务
curl -H "X-Tenant-Id: clinic-002" http://localhost:5000/api/transactions
```

### 健康检查
```bash
# 检查 MedLogicService 状态
curl http://localhost:5000/health

# 检查所有服务状态
docker-compose -f docker-compose.full-system.yml ps
```

## 🛠️ 开发和扩展

### 添加新设备
在 `appsettings.json` 中添加新的设备配置：

```json
{
  "SerialNumber": "MED-STATION-005",
  "TenantId": "new-tenant",
  "Location": "新科室",
  "Description": "新科室药品配送站"
}
```

### 自定义药品数据
修改 `MedDispenseSimulatorService.cs` 中的 `_medications` 列表：

```csharp
private readonly List<(string Code, string Name, decimal Price)> _medications = new()
{
    ("MED011", "新药品名称", 25.00m),
    // 添加更多药品...
};
```

### 自定义消息格式
修改 `MedDispenseMessage` 模型来适应不同的消息格式需求。

## 🐛 故障排除

### 常见问题

1. **连接失败**
   - 检查 MedLogicService 是否运行
   - 验证网络连接和端口

2. **租户不存在**
   - 确保在 TenantCatalogService 中创建了对应的租户
   - 检查租户ID是否正确

3. **数据库连接失败**
   - 检查 SQL Server 是否运行
   - 验证连接字符串配置

4. **权限问题**
   - 确保数据库用户有足够权限
   - 检查租户数据库是否已创建

### 调试模式
设置环境变量启用详细日志：
```bash
export Logging__LogLevel__Default=Debug
```

## 📈 性能调优

### 批量优化
- 增加 `BatchSize` 提高吞吐量
- 调整 `IntervalSeconds` 控制发送频率

### 并发控制
- 模拟器使用异步处理，支持高并发
- 可以运行多个模拟器实例

### 资源监控
- 监控 CPU 和内存使用情况
- 观察数据库连接数和响应时间

## 🔒 安全注意事项

- 生产环境中使用强密码
- 启用 HTTPS 连接
- 限制网络访问权限
- 定期更新依赖包