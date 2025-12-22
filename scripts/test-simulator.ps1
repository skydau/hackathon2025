# 测试 MedDispense 模拟器功能
# PowerShell 脚本

param(
    [string]$SmartGatewayUrl = "http://localhost:30000",
    [string]$DeviceId = "MED-STATION-001",
    [string]$DeviceRegistryUrl = "http://localhost:30081"
)

Write-Host "🧪 测试 MedDispense 模拟器 (通过 Smart Gateway)" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Cyan

# 1. 检查 Smart Gateway 连接
Write-Host "1. 检查 Smart Gateway 连接..." -ForegroundColor Yellow
try {
    $healthResponse = Invoke-RestMethod -Uri "$SmartGatewayUrl/health" -Method Get -TimeoutSec 5
    Write-Host "   ✓ Smart Gateway 健康检查通过" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Smart Gateway 连接失败: $_" -ForegroundColor Red
    exit 1
}

# 1.5. 检查设备注册
Write-Host "1.5. 检查设备注册..." -ForegroundColor Yellow
try {
    $deviceResponse = Invoke-RestMethod -Uri "$DeviceRegistryUrl/api/devices/$DeviceId/tenant" -Method Get -TimeoutSec 5
    $tenantId = $deviceResponse.tenantId
    Write-Host "   ✓ 设备 $DeviceId 已注册到租户: $tenantId" -ForegroundColor Green
} catch {
    Write-Host "   ✗ 设备 $DeviceId 未注册或无法访问: $_" -ForegroundColor Red
    Write-Host "   请先运行: .\scripts\register-devices.ps1" -ForegroundColor Yellow
    exit 1
}

# 2. 查看初始事务数据（通过 Smart Gateway）
Write-Host "2. 查看租户 $tenantId 的初始事务数据..." -ForegroundColor Yellow
try {
    $headers = @{ "Device-Id" = $DeviceId }
    $initialTransactions = Invoke-RestMethod -Uri "$SmartGatewayUrl/api/transactions" -Method Get -Headers $headers -TimeoutSec 10
    $initialCount = $initialTransactions.Count
    Write-Host "   ✓ 当前事务数量: $initialCount" -ForegroundColor Green
} catch {
    Write-Host "   ✗ 获取初始事务数据失败: $_" -ForegroundColor Red
    $initialCount = 0
}

# 3. 发送测试事务（通过 Smart Gateway）
Write-Host "3. 发送测试事务..." -ForegroundColor Yellow
$testTransaction = @{
    Amount = 125.50
    Description = "测试事务 - 药品配送模拟 (通过 Smart Gateway)"
}

try {
    $headers = @{ 
        "Device-Id" = $DeviceId
        "Content-Type" = "application/json"
    }
    $response = Invoke-RestMethod -Uri "$SmartGatewayUrl/api/transactions" -Method Post -Headers $headers -Body ($testTransaction | ConvertTo-Json) -TimeoutSec 10
    Write-Host "   ✓ 测试事务创建成功" -ForegroundColor Green
    Write-Host "     事务ID: $($response.Id)" -ForegroundColor White
    Write-Host "     金额: ¥$($response.Amount)" -ForegroundColor White
    Write-Host "     描述: $($response.Description)" -ForegroundColor White
} catch {
    Write-Host "   ✗ 创建测试事务失败: $_" -ForegroundColor Red
}

# 4. 验证事务数据
Write-Host "4. 验证事务数据..." -ForegroundColor Yellow
Start-Sleep -Seconds 2
try {
    $headers = @{ "Device-Id" = $DeviceId }
    $finalTransactions = Invoke-RestMethod -Uri "$SmartGatewayUrl/api/transactions" -Method Get -Headers $headers -TimeoutSec 10
    $finalCount = $finalTransactions.Count
    
    if ($finalCount -gt $initialCount) {
        Write-Host "   ✓ 事务数据验证成功" -ForegroundColor Green
        Write-Host "     新增事务数量: $($finalCount - $initialCount)" -ForegroundColor White
        
        # 显示最新的几个事务
        Write-Host "   最新事务:" -ForegroundColor Cyan
        $finalTransactions | Select-Object -First 3 | ForEach-Object {
            Write-Host "     ID: $($_.Id), 金额: ¥$($_.Amount), 时间: $($_.CreatedAt)" -ForegroundColor White
        }
    } else {
        Write-Host "   ⚠️  事务数量未增加，可能存在问题" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ✗ 验证事务数据失败: $_" -ForegroundColor Red
}

# 5. 测试建议
Write-Host ""
Write-Host "🎯 测试建议:" -ForegroundColor Cyan
Write-Host "1. 注册设备: .\scripts\register-devices.ps1" -ForegroundColor White
Write-Host "2. 运行模拟器: .\scripts\run-simulator.ps1" -ForegroundColor White
Write-Host "3. 观察日志输出，确认事务正在创建" -ForegroundColor White
Write-Host "4. 定期运行此测试脚本验证数据" -ForegroundColor White
Write-Host "5. 检查不同设备和租户的数据隔离" -ForegroundColor White

Write-Host ""
Write-Host "✅ 测试完成!" -ForegroundColor Green