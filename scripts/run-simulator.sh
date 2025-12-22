#!/bin/bash
# 运行 MedDispense 配送站模拟器
# Bash 脚本

# 默认参数
MEDLOGIC_URL="http://localhost:5000"
INTERVAL_SECONDS=5
BATCH_SIZE=2

# 解析命令行参数
while [[ $# -gt 0 ]]; do
    case $1 in
        --url)
            MEDLOGIC_URL="$2"
            shift 2
            ;;
        --interval)
            INTERVAL_SECONDS="$2"
            shift 2
            ;;
        --batch)
            BATCH_SIZE="$2"
            shift 2
            ;;
        -h|--help)
            echo "用法: $0 [选项]"
            echo "选项:"
            echo "  --url URL        MedLogicService URL (默认: http://localhost:5000)"
            echo "  --interval SEC   发送间隔秒数 (默认: 5)"
            echo "  --batch SIZE     批量大小 (默认: 2)"
            echo "  -h, --help       显示帮助信息"
            exit 0
            ;;
        *)
            echo "未知参数: $1"
            exit 1
            ;;
    esac
done

echo "🏥 启动 MedDispense 配送站模拟器"
echo "========================================"

# 检查 MedLogicService 是否可访问
echo "检查 MedLogicService 连接..."
if curl -f "$MEDLOGIC_URL/health" > /dev/null 2>&1; then
    echo "✓ MedLogicService 连接正常"
else
    echo "✗ 无法连接到 MedLogicService ($MEDLOGIC_URL)"
    echo "请确保 MedLogicService 正在运行"
    exit 1
fi

# 设置环境变量
export MedLogicService__BaseUrl="$MEDLOGIC_URL"
export Simulation__IntervalSeconds="$INTERVAL_SECONDS"
export Simulation__BatchSize="$BATCH_SIZE"

echo "配置参数:"
echo "  MedLogicService URL: $MEDLOGIC_URL"
echo "  发送间隔: $INTERVAL_SECONDS 秒"
echo "  批量大小: $BATCH_SIZE 个事务"
echo ""

# 构建并运行模拟器
echo "构建模拟器..."
cd tools/MedDispenseSimulator

if dotnet build --configuration Release; then
    echo "✓ 构建成功"
    echo ""
    echo "🚀 启动模拟器..."
    echo "按 Ctrl+C 停止模拟器"
    echo ""
    
    dotnet run --configuration Release
else
    echo "✗ 构建失败"
    exit 1
fi