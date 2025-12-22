# 运行 MedDispense 配送站模拟器
# PowerShell 脚本

param(
    [string]$SmartGatewayUrl = "http://localhost:30000",
    [int]$IntervalSeconds = 5,
    [int]$BatchSize = 2
)

Write-Host "🏥 启动 MedDispense 配送站模拟器" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan

# 检查 Smart Gateway 是否可访问
Write-Host "检查 Smart Gateway 连接..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$SmartGatewayUrl/health" -TimeoutSec 5 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-Host "✓ Smart Gateway 连接正常" -ForegroundColor Green
    }
} catch {
    Write-Host "✗ 无法连接到 Smart Gateway ($SmartGatewayUrl)" -ForegroundColor Red
    Write-Host "请确保 Smart Gateway 正在运行" -ForegroundColor Yellow
    exit 1
}

# 设置环境变量
$env:SmartGateway__BaseUrl = $SmartGatewayUrl
$env:Simulation__IntervalSeconds = $IntervalSeconds
$env:Simulation__BatchSize = $BatchSize

Write-Host "配置参数:" -ForegroundColor Cyan
Write-Host "  Smart Gateway URL: $SmartGatewayUrl" -ForegroundColor White
Write-Host "  发送间隔: $IntervalSeconds 秒" -ForegroundColor White
Write-Host "  批量大小: $BatchSize 个事务" -ForegroundColor White
Write-Host ""

# 构建并运行模拟器
Write-Host "构建模拟器..." -ForegroundColor Yellow
Set-Location "tools/MedDispenseSimulator"

try {
    dotnet build --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "构建失败"
    }
    
    Write-Host "✓ 构建成功" -ForegroundColor Green
    Write-Host ""
    Write-Host "🚀 启动模拟器..." -ForegroundColor Green
    Write-Host "按 Ctrl+C 停止模拟器" -ForegroundColor Yellow
    Write-Host ""
    
    dotnet run --configuration Release
} catch {
    Write-Host "✗ 启动失败: $_" -ForegroundColor Red
    exit 1
} finally {
    Set-Location "../.."
}