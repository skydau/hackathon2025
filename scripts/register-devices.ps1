# 注册模拟设备到 Device Registry
# PowerShell 脚本

param(
    [string]$DeviceRegistryUrl = "http://localhost:30081",
    [string]$ConfigFile = "tools/MedDispenseSimulator/appsettings.json"
)

Write-Host "🔧 注册模拟设备到 Device Registry" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan

# 检查 Device Registry 连接
Write-Host "检查 Device Registry 连接..." -ForegroundColor Yellow
try {
    $healthResponse = Invoke-RestMethod -Uri "$DeviceRegistryUrl/health" -Method Get -TimeoutSec 5
    Write-Host "✓ Device Registry 连接正常" -ForegroundColor Green
} catch {
    Write-Host "✗ 无法连接到 Device Registry ($DeviceRegistryUrl)" -ForegroundColor Red
    Write-Host "请确保 Device Registry 正在运行" -ForegroundColor Yellow
    exit 1
}

# 读取配置文件中的设备信息
if (-not (Test-Path $ConfigFile)) {
    Write-Host "✗ 配置文件不存在: $ConfigFile" -ForegroundColor Red
    exit 1
}

try {
    $config = Get-Content $ConfigFile | ConvertFrom-Json
    $devices = $config.Devices
    
    if (-not $devices -or $devices.Count -eq 0) {
        Write-Host "✗ 配置文件中没有找到设备信息" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "找到 $($devices.Count) 个设备配置" -ForegroundColor Green
} catch {
    Write-Host "✗ 读取配置文件失败: $_" -ForegroundColor Red
    exit 1
}

# 注册每个设备
$successCount = 0
$failCount = 0

foreach ($device in $devices) {
    Write-Host ""
    Write-Host "注册设备: $($device.SerialNumber)" -ForegroundColor Yellow
    Write-Host "  租户ID: $($device.TenantId)" -ForegroundColor White
    Write-Host "  位置: $($device.Location)" -ForegroundColor White
    Write-Host "  描述: $($device.Description)" -ForegroundColor White
    
    # 构造设备注册请求
    $deviceRequest = @{
        serialNumber = $device.SerialNumber
        tenantId = $device.TenantId
        deviceType = "MedDispenseStation"
        location = $device.Location
        description = $device.Description
    }
    
    try {
        # 先检查设备是否已存在
        try {
            $existingDevice = Invoke-RestMethod -Uri "$DeviceRegistryUrl/api/devices/$($device.SerialNumber)" -Method Get -TimeoutSec 10
            Write-Host "  ⚠️  设备已存在，跳过注册" -ForegroundColor Yellow
            $successCount++
            continue
        } catch {
            # 设备不存在，继续注册
        }
        
        # 注册设备
        $response = Invoke-RestMethod -Uri "$DeviceRegistryUrl/api/devices" -Method Post -Body ($deviceRequest | ConvertTo-Json) -ContentType "application/json" -TimeoutSec 10
        
        Write-Host "  ✓ 设备注册成功" -ForegroundColor Green
        $successCount++
        
    } catch {
        Write-Host "  ✗ 设备注册失败: $_" -ForegroundColor Red
        $failCount++
    }
}

# 验证注册结果
Write-Host ""
Write-Host "📊 注册结果统计:" -ForegroundColor Cyan
Write-Host "  成功: $successCount" -ForegroundColor Green
Write-Host "  失败: $failCount" -ForegroundColor Red

if ($failCount -eq 0) {
    Write-Host ""
    Write-Host "🎉 所有设备注册完成!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📋 验证命令:" -ForegroundColor Cyan
    Write-Host "  查看所有设备: curl $DeviceRegistryUrl/api/devices" -ForegroundColor White
    Write-Host "  查看特定设备: curl $DeviceRegistryUrl/api/devices/MED-STATION-001" -ForegroundColor White
    Write-Host "  查看设备租户: curl $DeviceRegistryUrl/api/devices/MED-STATION-001/tenant" -ForegroundColor White
    Write-Host ""
    Write-Host "🚀 现在可以启动模拟器:" -ForegroundColor Cyan
    Write-Host "  .\scripts\run-simulator.ps1" -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "⚠️  部分设备注册失败，请检查错误信息" -ForegroundColor Yellow
}