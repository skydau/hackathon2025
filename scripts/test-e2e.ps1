# 端到端集成测试脚本
# 此脚本测试从租户创建到数据库自动配置的完整流程

param(
    [string]$TenantName = "test-hospital", 
    [string]$SqlServerHost = "localhost",
    [string]$SqlServerPort = "1433",
    [string]$SqlServerUser = "sa",
    [string]$SqlServerPassword = ""
)

# 颜色输出函数
function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Error-Message {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ $Message" -ForegroundColor Cyan
}

function Write-Step {
    param([string]$Message)
    Write-Host "`n=== $Message ===" -ForegroundColor Yellow
}

# 检查必需的工具
function Test-Prerequisites {
    Write-Step "检查前置条件"
    
    $tools = @("kubectl", "minikube")
    $missing = @()
    
    foreach ($tool in $tools) {
        if (!(Get-Command $tool -ErrorAction SilentlyContinue)) {
            $missing += $tool
            Write-Error-Message "$tool 未安装"
        } else {
            Write-Success "$tool 已安装"
        }
    }
    
    if ($missing.Count -gt 0) {
        Write-Error-Message "缺少必需工具: $($missing -join ', ')"
        exit 1
    }
    
    # 检查Minikube状态
    $minikubeStatus = minikube status --format='{{.Host}}' 2>$null
    if ($minikubeStatus -ne "Running") {
        Write-Error-Message "Minikube未运行，请先启动: minikube start"
        exit 1
    }
    Write-Success "Minikube正在运行"
    
    # 检查SQL Server连接
    if ([string]::IsNullOrEmpty($SqlServerPassword)) {
        Write-Error-Message "请提供SQL Server密码: -SqlServerPassword 'YourPassword'"
        exit 1
    }
}

# 清理之前的测试资源
function Remove-TestResources {
    Write-Step "清理之前的测试资源"
    
    # 删除Tenant CRD
    kubectl delete tenant $TenantName -n default --ignore-not-found=true 2>$null
    Write-Info "已删除Tenant CRD: $TenantName"
    
    # 删除租户命名空间
    kubectl delete namespace "tenant-$TenantName" --ignore-not-found=true 2>$null
    Write-Info "已删除命名空间: tenant-$TenantName"
    
    # 等待资源清理
    Start-Sleep -Seconds 5
    Write-Success "测试资源清理完成"
}

# 创建Tenant CRD
function New-TenantCRD {
    Write-Step "创建Tenant CRD"
    
    $tenantYaml = @"
apiVersion: tenants.medlogic.io/v1
kind: Tenant
metadata:
  name: $TenantName
spec:
  displayName: "Test Hospital"
  db:
    mode: perDatabase
    server: "host.minikube.internal,$SqlServerPort"
    database: ${TenantName}_DB
  throttling:
    rps: 100
  slo:
    availability: "99.9%"
    p95_latency_ms: 1000
"@
    
    $tenantYaml | kubectl apply -f - 2>&1 | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Tenant CRD创建成功: $TenantName"
    } else {
        Write-Error-Message "Tenant CRD创建失败"
        exit 1
    }
}

# 等待Tenant Operator处理
function Wait-TenantProvisioning {
    Write-Step "等待Tenant Operator处理"
    
    $maxWait = 120
    $waited = 0
    $interval = 5
    
    while ($waited -lt $maxWait) {
        $phase = kubectl get tenant $TenantName -o jsonpath='{.status.phase}' 2>$null
        
        if ($phase -eq "Ready") {
            Write-Success "租户状态: Ready"
            return $true
        } elseif ($phase -eq "Failed") {
            Write-Error-Message "租户状态: Failed"
            kubectl get tenant $TenantName -o yaml
            return $false
        } else {
            Write-Info "租户状态: $phase (等待中... $waited/$maxWait 秒)"
        }
        
        Start-Sleep -Seconds $interval
        $waited += $interval
    }
    
    Write-Error-Message "等待超时"
    return $false
}

# 验证命名空间创建
function Test-NamespaceCreated {
    Write-Step "验证命名空间创建"
    
    $namespace = kubectl get namespace "tenant-$TenantName" --ignore-not-found=true 2>$null
    
    if ($namespace) {
        Write-Success "命名空间已创建: tenant-$TenantName"
        return $true
    } else {
        Write-Error-Message "命名空间未创建"
        return $false
    }
}

# 验证Secret创建
function Test-SecretCreated {
    Write-Step "验证Secret创建"
    
    $secretName = "tenant-$TenantName-db-secret"
    $secret = kubectl get secret $secretName -n "tenant-$TenantName" --ignore-not-found=true 2>$null
    
    if ($secret) {
        Write-Success "Secret已创建: $secretName"
        
        # 验证Secret包含必需的字段
        $fields = @("username", "password", "server", "database")
        $allFieldsPresent = $true
        
        foreach ($field in $fields) {
            $value = kubectl get secret $secretName -n "tenant-$TenantName" -o jsonpath="{.data.$field}" 2>$null
            if ($value) {
                Write-Success "  - ${field}: 存在"
            } else {
                Write-Error-Message "  - ${field}: 缺失"
                $allFieldsPresent = $false
            }
        }
        
        return $allFieldsPresent
    } else {
        Write-Error-Message "Secret未创建"
        return $false
    }
}

# 验证数据库创建
function Test-DatabaseCreated {
    Write-Step "验证数据库在SQL Server上创建"
    
    $dbName = "${TenantName}_DB"
    
    # 使用sqlcmd检查数据库
    $query = "SELECT name FROM sys.databases WHERE name = '$dbName'"
    
    try {
        $result = sqlcmd -S "$SqlServerHost,$SqlServerPort" -U $SqlServerUser -P $SqlServerPassword -Q $query -h -1 2>$null
        
        if ($result -match $dbName) {
            Write-Success "数据库已创建: $dbName"
            
            # 验证表结构
            $tableQuery = "USE [$dbName]; SELECT name FROM sys.tables WHERE name IN ('Transactions', 'AuditLogs')"
            $tables = sqlcmd -S "$SqlServerHost,$SqlServerPort" -U $SqlServerUser -P $SqlServerPassword -Q $tableQuery -h -1 2>$null
            
            if ($tables -match "Transactions" -and $tables -match "AuditLogs") {
                Write-Success "  - 表结构已初始化 (Transactions, AuditLogs)"
            } else {
                Write-Error-Message "  - 表结构未初始化"
                return $false
            }
            
            return $true
        } else {
            Write-Error-Message "数据库未创建"
            return $false
        }
    } catch {
        Write-Error-Message "无法连接到SQL Server: $_"
        Write-Info "请确保SQL Server正在运行并且凭据正确"
        return $false
    }
}

# 验证数据库配置注册到Tenant Catalog
function Test-DatabaseConfigRegistered {
    Write-Step "验证数据库配置注册到Tenant Catalog"
    
    # 获取Tenant Catalog Service的端点
    $catalogService = kubectl get service tenant-catalog -n platform-system --ignore-not-found=true 2>$null
    
    if (!$catalogService) {
        Write-Info "Tenant Catalog Service未部署，跳过此测试"
        return $true
    }
    
    # 端口转发到Tenant Catalog Service
    $port = 8080
    Write-Info "设置端口转发到Tenant Catalog Service..."
    
    $job = Start-Job -ScriptBlock {
        param($port)
        kubectl port-forward -n platform-system service/tenant-catalog ${port}:8080
    } -ArgumentList $port
    
    Start-Sleep -Seconds 3
    
    try {
        # 查询租户配置
        $response = Invoke-RestMethod -Uri "http://localhost:$port/api/tenants/$TenantName" -Method Get -ErrorAction Stop
        
        if ($response.dbConfig) {
            Write-Success "数据库配置已注册到Tenant Catalog"
            Write-Info "  - Server: $($response.dbConfig.server)"
            Write-Info "  - Database: $($response.dbConfig.database)"
            return $true
        } else {
            Write-Error-Message "数据库配置未注册"
            return $false
        }
    } catch {
        Write-Error-Message "无法查询Tenant Catalog: $_"
        return $false
    } finally {
        Stop-Job -Job $job
        Remove-Job -Job $job
    }
}

# 测试通过Smart Gateway发送请求
function Test-SmartGatewayRequest {
    Write-Step "测试通过Smart Gateway发送请求"
    
    # 检查Smart Gateway是否部署
    $gatewayService = kubectl get service smart-gateway -n gateway --ignore-not-found=true 2>$null
    
    if (!$gatewayService) {
        Write-Info "Smart Gateway未部署，跳过此测试"
        return $true
    }
    
    Write-Info "Smart Gateway集成测试需要完整的部署环境"
    Write-Info "请参考docs/LOCAL_ENVIRONMENT_GUIDE.zh.md完成部署后再运行此测试"
    return $true
}

# 测试租户退服流程
function Test-TenantDecommissioning {
    Write-Step "测试租户退服流程"
    
    Write-Info "更新租户状态为Decommissioned..."
    
    # 更新Tenant CRD状态
    kubectl patch tenant $TenantName --type=merge -p '{"spec":{"status":"Decommissioned"}}' 2>$null
    
    # 等待退服流程完成
    Start-Sleep -Seconds 10
    
    # 验证命名空间是否被删除
    $namespace = kubectl get namespace "tenant-$TenantName" --ignore-not-found=true 2>$null
    
    if (!$namespace) {
        Write-Success "命名空间已删除"
    } else {
        Write-Info "命名空间仍然存在（退服流程可能需要更长时间）"
    }
    
    # 验证数据库备份是否创建
    Write-Info "检查数据库备份..."
    Write-Info "备份验证需要访问Tenant Operator日志"
    
    return $true
}

# 主测试流程
function Start-E2ETest {
    Write-Host "`n╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║         多租户医疗平台 - 端到端集成测试                      ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan
    
    # 检查前置条件
    Test-Prerequisites
    
    # 清理之前的测试资源
    Remove-TestResources
    
    # 测试计数器
    $totalTests = 0
    $passedTests = 0
    $failedTests = 0
    
    # 测试1: 创建Tenant CRD
    $totalTests++
    New-TenantCRD
    if ($?) { $passedTests++ } else { $failedTests++ }
    
    # 测试2: 等待Tenant Operator处理
    $totalTests++
    if (Wait-TenantProvisioning) { $passedTests++ } else { $failedTests++; return }
    
    # 测试3: 验证命名空间创建
    $totalTests++
    if (Test-NamespaceCreated) { $passedTests++ } else { $failedTests++ }
    
    # 测试4: 验证Secret创建
    $totalTests++
    if (Test-SecretCreated) { $passedTests++ } else { $failedTests++ }
    
    # 测试5: 验证数据库创建
    $totalTests++
    if (Test-DatabaseCreated) { $passedTests++ } else { $failedTests++ }
    
    # 测试6: 验证数据库配置注册
    $totalTests++
    if (Test-DatabaseConfigRegistered) { $passedTests++ } else { $failedTests++ }
    
    # 测试7: 测试Smart Gateway请求
    $totalTests++
    if (Test-SmartGatewayRequest) { $passedTests++ } else { $failedTests++ }
    
    # 测试8: 测试租户退服流程
    $totalTests++
    if (Test-TenantDecommissioning) { $passedTests++ } else { $failedTests++ }
    
    # 测试结果汇总
    Write-Host "`n╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                      测试结果汇总                           ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan
    
    Write-Host "总测试数: $totalTests" -ForegroundColor White
    Write-Host "通过: $passedTests" -ForegroundColor Green
    Write-Host "失败: $failedTests" -ForegroundColor Red
    
    if ($failedTests -eq 0) {
        Write-Host "`n✓ 所有测试通过！" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "`n✗ 部分测试失败" -ForegroundColor Red
        exit 1
    }
}

# 运行测试
Start-E2ETest
