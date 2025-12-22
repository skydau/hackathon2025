#!/bin/bash
# 注册模拟设备到 Device Registry
# Bash 脚本

DEVICE_REGISTRY_URL="http://localhost:30081"
CONFIG_FILE="tools/MedDispenseSimulator/appsettings.json"

# 解析命令行参数
while [[ $# -gt 0 ]]; do
    case $1 in
        --url)
            DEVICE_REGISTRY_URL="$2"
            shift 2
            ;;
        --config)
            CONFIG_FILE="$2"
            shift 2
            ;;
        -h|--help)
            echo "用法: $0 [选项]"
            echo "选项:"
            echo "  --url URL        Device Registry URL (默认: http://localhost:30081)"
            echo "  --config FILE    配置文件路径 (默认: tools/MedDispenseSimulator/appsettings.json)"
            echo "  -h, --help       显示帮助信息"
            exit 0
            ;;
        *)
            echo "未知参数: $1"
            exit 1
            ;;
    esac
done

echo "🔧 注册模拟设备到 Device Registry"
echo "========================================"

# 检查 Device Registry 连接
echo "检查 Device Registry 连接..."
if curl -f "$DEVICE_REGISTRY_URL/health" > /dev/null 2>&1; then
    echo "✓ Device Registry 连接正常"
else
    echo "✗ 无法连接到 Device Registry ($DEVICE_REGISTRY_URL)"
    echo "请确保 Device Registry 正在运行"
    exit 1
fi

# 检查配置文件
if [ ! -f "$CONFIG_FILE" ]; then
    echo "✗ 配置文件不存在: $CONFIG_FILE"
    exit 1
fi

# 检查 jq 工具
if ! command -v jq &> /dev/null; then
    echo "✗ 需要安装 jq 工具来解析 JSON"
    echo "Ubuntu/Debian: sudo apt-get install jq"
    echo "CentOS/RHEL: sudo yum install jq"
    echo "macOS: brew install jq"
    exit 1
fi

# 读取设备配置
DEVICES=$(jq -r '.Devices[] | @base64' "$CONFIG_FILE" 2>/dev/null)
if [ -z "$DEVICES" ]; then
    echo "✗ 配置文件中没有找到设备信息"
    exit 1
fi

DEVICE_COUNT=$(echo "$DEVICES" | wc -l)
echo "找到 $DEVICE_COUNT 个设备配置"

# 注册每个设备
SUCCESS_COUNT=0
FAIL_COUNT=0

while IFS= read -r device_data; do
    # 解码设备信息
    DEVICE=$(echo "$device_data" | base64 --decode)
    SERIAL_NUMBER=$(echo "$DEVICE" | jq -r '.SerialNumber')
    TENANT_ID=$(echo "$DEVICE" | jq -r '.TenantId')
    LOCATION=$(echo "$DEVICE" | jq -r '.Location')
    DESCRIPTION=$(echo "$DEVICE" | jq -r '.Description')
    
    echo ""
    echo "注册设备: $SERIAL_NUMBER"
    echo "  租户ID: $TENANT_ID"
    echo "  位置: $LOCATION"
    echo "  描述: $DESCRIPTION"
    
    # 检查设备是否已存在
    if curl -f "$DEVICE_REGISTRY_URL/api/devices/$SERIAL_NUMBER" > /dev/null 2>&1; then
        echo "  ⚠️  设备已存在，跳过注册"
        ((SUCCESS_COUNT++))
        continue
    fi
    
    # 构造注册请求
    REQUEST_BODY=$(jq -n \
        --arg serialNumber "$SERIAL_NUMBER" \
        --arg tenantId "$TENANT_ID" \
        --arg deviceType "MedDispenseStation" \
        --arg location "$LOCATION" \
        --arg description "$DESCRIPTION" \
        '{
            serialNumber: $serialNumber,
            tenantId: $tenantId,
            deviceType: $deviceType,
            location: $location,
            description: $description
        }')
    
    # 注册设备
    if curl -f -X POST "$DEVICE_REGISTRY_URL/api/devices" \
        -H "Content-Type: application/json" \
        -d "$REQUEST_BODY" > /dev/null 2>&1; then
        echo "  ✓ 设备注册成功"
        ((SUCCESS_COUNT++))
    else
        echo "  ✗ 设备注册失败"
        ((FAIL_COUNT++))
    fi
    
done <<< "$DEVICES"

# 显示结果
echo ""
echo "📊 注册结果统计:"
echo "  成功: $SUCCESS_COUNT"
echo "  失败: $FAIL_COUNT"

if [ $FAIL_COUNT -eq 0 ]; then
    echo ""
    echo "🎉 所有设备注册完成!"
    echo ""
    echo "📋 验证命令:"
    echo "  查看所有设备: curl $DEVICE_REGISTRY_URL/api/devices"
    echo "  查看特定设备: curl $DEVICE_REGISTRY_URL/api/devices/MED-STATION-001"
    echo "  查看设备租户: curl $DEVICE_REGISTRY_URL/api/devices/MED-STATION-001/tenant"
    echo ""
    echo "🚀 现在可以启动模拟器:"
    echo "  ./scripts/run-simulator.sh"
else
    echo ""
    echo "⚠️  部分设备注册失败，请检查错误信息"
fi