# MedLogic 平台 - 部署文档摘要

本文档提供了为 MedLogic 多租户医疗平台创建的所有部署文档和脚本的概述。

## 📚 已创建的文档

### 核心文档

1. **[docs/DEPLOYMENT_GUIDE.md](docs/DEPLOYMENT_GUIDE.md)**
   - 全面的部署说明
   - 先决条件和 Azure 资源设置
   - 多种部署方法（Helm、kubectl、自动化脚本）
   - 部署后配置
   - 验证程序
   - 回滚说明

2. **[docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md)**
   - 常见部署问题的解决方案
   - 服务健康诊断
   - 数据库连接问题
   - Workload Identity 故障排除
   - 网络和路由问题
   - 性能优化
   - 安全和策略问题

3. **[docs/QUICK_START.md](docs/QUICK_START.md)**
   - 快速部署指南
   - 最少的入门步骤
   - 快速验证程序
   - 首个租户创建
   - 清理说明

4. **[docs/DEPLOYMENT_CHECKLIST.md](docs/DEPLOYMENT_CHECKLIST.md)**
   - 完整的部署前检查清单
   - 构建和部署验证
   - 部署后任务
   - 生产就绪标准
   - 签核程序

5. **[docs/README.md](docs/README.md)**
   - 文档索引
   - 所有资源的快速链接
   - 架构概述
   - 支持信息

## 🚀 部署脚本

### 1. 自动化部署脚本
**位置**: `scripts/deploy.sh`

**用途**: 完全自动化部署整个平台

**功能**:
- 环境选择（开发/预发布/生产）
- 自定义配置文件支持
- 可选的跳过构建
- 可选的跳过测试
- 试运行模式
- 自动健康验证
- 部署摘要

**使用方法**:
```bash
./scripts/deploy.sh --environment production --config values-production.yaml
```

**选项**:
- `-e, --environment`: 部署环境
- `-c, --config`: 配置文件路径
- `-s, --skip-build`: 跳过构建镜像
- `-t, --skip-tests`: 跳过运行测试
- `-d, --dry-run`: 执行试运行
- `--timeout`: Helm 超时时长

### 2. 构建和推送脚本
**位置**: `scripts/build-and-push.sh`

**用途**: 构建所有容器镜像并推送到 Azure 容器注册表

**功能**:
- 构建所有 .NET 服务
- 构建 NGINX 网关
- 构建基于 Go 的租户操作器
- 推送所有镜像到 ACR
- 版本标签支持

**使用方法**:
```bash
./scripts/build-and-push.sh medlogicacr.azurecr.io [version]
```

### 3. 端到端测试脚本
**位置**: `scripts/e2e-test.sh`

**用途**: 验证完整的平台功能

**测试内容**:
- 租户创建
- 租户检索
- 设备注册
- 设备到租户的映射
- 租户状态更新
- 速率限制
- 租户隔离

**使用方法**:
```bash
./scripts/e2e-test.sh
```

### 4. 诊断收集脚本
**位置**: `scripts/collect-diagnostics.sh`

**用途**: 收集全面的诊断信息用于故障排除

**收集内容**:
- 集群信息
- Pod 日志和描述
- 服务配置
- 事件
- 资源使用情况
- 网络策略
- CRD 和自定义资源
- Helm 发布

**使用方法**:
```bash
./scripts/collect-diagnostics.sh
```

**输出**: 创建带时间戳的 tar.gz 归档文件，包含所有诊断信息

## 📦 Helm 图表

### 图表结构
**位置**: `helm/medlogic-platform/`

```
helm/medlogic-platform/
├── Chart.yaml                              # Chart metadata
├── values.yaml                             # Default configuration
├── values-production-sample.yaml           # Production example
└── templates/
    ├── _helpers.tpl                        # Template helpers
    ├── namespaces.yaml                     # Namespace definitions
    ├── tenant-catalog-deployment.yaml      # Tenant Catalog Service
    ├── device-registry-deployment.yaml     # Device Registry Service
    └── smart-gateway-deployment.yaml       # Smart Gateway
```

### Key Features

1. **Configurable Components**:
   - All services can be enabled/disabled
   - Replica counts configurable
   - Resource limits customizable
   - Health check parameters tunable

2. **Azure Integration**:
   - Workload Identity support
   - Key Vault integration
   - SQL Server configuration
   - ACR image pulling

3. **Security**:
   - Network policies
   - RBAC configuration
   - Service accounts with annotations
   - TLS support (optional)

4. **Observability**:
   - Prometheus integration
   - Loki integration
   - Grafana dashboards
   - SLO monitoring

### Deployment with Helm

```bash
# Install
helm install medlogic-platform ./helm/medlogic-platform \
  --values values-production.yaml \
  --namespace platform-system \
  --create-namespace

# Upgrade
helm upgrade medlogic-platform ./helm/medlogic-platform \
  --values values-production.yaml \
  --namespace platform-system

# Rollback
helm rollback medlogic-platform -n platform-system
```

## 🔧 Configuration

### Sample Configuration Files

1. **values-production-sample.yaml**: Production-ready configuration template
   - High availability settings (3+ replicas)
   - Production resource limits
   - Azure Workload Identity configuration
   - Observability stack enabled
   - Security features enabled

### Required Configuration

Before deployment, customize:

1. **Image Registry**: Your ACR URL
2. **Azure Resources**:
   - Client IDs for Workload Identity
   - Tenant IDs
   - SQL Server names
   - Key Vault names
   - Subscription IDs
3. **Database Configuration**:
   - Server endpoints
   - Database names
4. **Resource Limits**: Based on your workload

## 📋 Deployment Workflow

### Standard Deployment Process

1. **Pre-Deployment**
   ```bash
   # Set environment variables
   export RESOURCE_GROUP="medlogic-prod-rg"
   export AKS_CLUSTER="medlogic-prod-aks"
   export ACR_NAME="medlogicprodacr"
   
   # Create Azure resources
   az group create --name $RESOURCE_GROUP --location eastus
   az aks create --resource-group $RESOURCE_GROUP --name $AKS_CLUSTER ...
   az acr create --resource-group $RESOURCE_GROUP --name $ACR_NAME ...
   ```

2. **Build and Push Images**
   ```bash
   ./scripts/build-and-push.sh $ACR_NAME.azurecr.io 1.0.0
   ```

3. **Configure Workload Identity**
   ```bash
   cd k8s
   ./workload-identity-setup.sh
   ```

4. **Deploy Platform**
   ```bash
   ./scripts/deploy.sh --environment production --config values-production.yaml
   ```

5. **Verify Deployment**
   ```bash
   ./scripts/e2e-test.sh
   ```

6. **Monitor**
   ```bash
   kubectl get pods -n platform-system
   kubectl logs -n platform-system deployment/tenant-catalog-service
   ```

## 🔍 Verification Checklist

After deployment, verify:

- [ ] All pods are running
- [ ] Health endpoints responding
- [ ] Gateway has external IP
- [ ] Can create tenants
- [ ] Can register devices
- [ ] Database connectivity working
- [ ] Workload Identity functioning
- [ ] Network policies enforced
- [ ] Monitoring collecting metrics
- [ ] Logs being aggregated

## 🆘 Troubleshooting

### Quick Diagnostics

```bash
# Collect diagnostics
./scripts/collect-diagnostics.sh

# Check pod status
kubectl get pods --all-namespaces | grep -v Running

# Check logs
kubectl logs -n platform-system deployment/tenant-catalog-service --tail=100

# Check events
kubectl get events -n platform-system --sort-by='.lastTimestamp'
```

### Common Issues

1. **Pods not starting**: Check image pull secrets and ACR attachment
2. **Health checks failing**: Verify database connectivity and environment variables
3. **Gateway 502 errors**: Check service endpoints and DNS resolution
4. **Workload Identity errors**: Verify federated credentials and service account annotations

See [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) for detailed solutions.

## 📊 Monitoring

### Key Metrics to Monitor

- Pod health and restart counts
- Service response times
- Database connection pool usage
- Gateway request rates
- Error rates per tenant
- Resource utilization (CPU, memory)

### Accessing Monitoring

```bash
# Port forward to Grafana
kubectl port-forward -n observability svc/grafana 3000:3000

# Access at http://localhost:3000
```

## 🔐 Security Considerations

### Implemented Security Features

1. **Workload Identity**: Passwordless authentication to Azure resources
2. **Key Vault**: Centralized secret management
3. **Network Policies**: Strict network isolation between tenants
4. **OPA Gatekeeper**: Policy enforcement at admission time
5. **TDE**: Database encryption at rest
6. **RBAC**: Least-privilege access control

### Security Checklist

- [ ] No secrets in ConfigMaps or code
- [ ] All images scanned for vulnerabilities
- [ ] Network policies tested
- [ ] Gatekeeper constraints enforced
- [ ] Audit logging enabled
- [ ] Regular security reviews scheduled

## 📈 Scaling

### Horizontal Scaling

```bash
# Scale a deployment
kubectl scale deployment/tenant-catalog-service --replicas=5 -n platform-system

# Enable autoscaling
kubectl autoscale deployment/tenant-catalog-service \
  --min=3 --max=10 --cpu-percent=70 \
  -n platform-system
```

### Vertical Scaling

Update resource limits in values.yaml and upgrade Helm release.

## 🔄 Updates and Maintenance

### Updating Services

```bash
# Build new version
./scripts/build-and-push.sh medlogicacr.azurecr.io 1.1.0

# Update values.yaml with new tag
# Then upgrade
helm upgrade medlogic-platform ./helm/medlogic-platform \
  --values values-production.yaml \
  --namespace platform-system
```

### Rolling Back

```bash
# Helm rollback
helm rollback medlogic-platform -n platform-system

# Or rollback specific deployment
kubectl rollout undo deployment/tenant-catalog-service -n platform-system
```

## 📞 Support

### Getting Help

- **Documentation**: Start with [QUICK_START.md](docs/QUICK_START.md)
- **Troubleshooting**: See [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md)
- **Issues**: Create GitHub issue with diagnostics bundle
- **Email**: platform@medlogic.io

### Reporting Issues

Include:
1. Description of the problem
2. Steps to reproduce
3. Diagnostics bundle (`./scripts/collect-diagnostics.sh`)
4. Environment details

## ✅ Next Steps

After successful deployment:

1. Review [DEPLOYMENT_CHECKLIST.md](docs/DEPLOYMENT_CHECKLIST.md)
2. Configure monitoring and alerting
3. Set up CI/CD pipeline
4. Document tenant onboarding process
5. Conduct disaster recovery drill
6. Train operations team

## 📝 Summary

This deployment package provides:

- ✅ Comprehensive documentation (5 guides)
- ✅ Automated deployment scripts (4 scripts)
- ✅ Production-ready Helm chart
- ✅ Sample configurations
- ✅ Troubleshooting guides
- ✅ Verification procedures
- ✅ Security best practices

All components are ready for production deployment of the MedLogic Multi-Tenant Medical Platform.
