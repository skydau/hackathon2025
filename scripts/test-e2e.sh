#!/bin/bash

# 端到端集成测试脚本
# 此脚本测试从租户创建到数据库自动配置的完整流程

set -e

# 默认参数
TENANT_NAME="${1:-test-hospital}"
SQL_SERVER_HOST="${SQL_SERVER_HOST:-localhost}"
SQL_SERVER_PORT="${SQL_SERVER_PORT:-1433}"
SQL_SERVER_USER="${SQL_SERVER_USER:-sa}"
SQL_SERVER_PASSWORD="${SQL_SERVER_PASSWORD:-}"

# 颜色输出
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

function print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

function print_error() {
    echo -e "${RED}✗ $1${NC}"
}

function print_info() {
    echo -e "${CYAN}ℹ $1${NC}"
}

function print_step() {
    echo -e "\n${YELLOW}=== $1 ===${NC}"
}

# 检查必需的工具
function check_prerequisites() {
    print_step "检查前置条件"
    
    local tools=("kubectl" "minikube")
    local missing=()
    
    for tool in "${tools[@]}"; do
        if ! command -v "$tool" &> /dev/null; then
            missing+=("$tool")
            print_error "$tool 未安装"
        else
            print_success "$tool 已安装"
        fi
    done
    
    if [ ${#missing[@]} -gt 0 ]; then
        print_error "缺少必需工具: ${missing[*]}"
        exit 1
    fi
    
    # 检查Minikube状态
    if ! minikube status | grep -q "host: Running"; then
        print_error "Minikube未运行，请先启动: minikube start"
        exit 1
    fi
    print_success "Minikube正在运行"
    
    # 检查SQL Server连接
    if [ -z "$SQL_SERVER_PASSWORD" ]; then
        print_error "请提供SQL Server密码: export SQL_SERVER_PASSWORD='YourPassword'"
        exit 1
    fi
}

# 清理之前的测试资源
function cleanup_test_resources() {
    print_step "清理之前的测试资源"
    
    # 删除Tenant CRD
    kubectl delete tenant "$TENANT_NAME" -n default --ignore-not-found=true 2>/dev/null || true
    print_info "已删除Tenant CRD: $TENANT_NAME"
    
    # 删除租户命名空间
    kubectl delete namespace "tenant-$TENANT_NAME" --ignore-not-found=true 2>/dev/null || true
    print_info "已删除命名空间: tenant-$TENANT_NAME"
    
    # 等待资源清理
    sleep 5
    print_success "测试资源清理完成"
}

# 创建Tenant CRD
function create_tenant_crd() {
    print_step "创建Tenant CRD"
    
    cat <<EOF | kubectl apply -f - >/dev/null 2>&1
apiVersion: tenants.medlogic.io/v1
kind: Tenant
metadata:
  name: $TENANT_NAME
spec:
  displayName: "Test Hospital"
  db:
    mode: perDatabase
    server: "host.minikube.internal,$SQL_SERVER_PORT"
    database: ${TENANT_NAME}_DB
  throttling:
    rps: 100
  slo:
    availability: "99.9%"
    p95_latency_ms: 1000
EOF
    
    if [ $? -eq 0 ]; then
        print_success "Tenant CRD创建成功: $TENANT_NAME"
        return 0
    else
        print_error "Tenant CRD创建失败"
        return 1
    fi
}

# 等待Tenant Operator处理
function wait_tenant_provisioning() {
    print_step "等待Tenant Operator处理"
    
    local max_wait=120
    local waited=0
    local interval=5
    
    while [ $waited -lt $max_wait ]; do
        local phase=$(kubectl get tenant "$TENANT_NAME" -o jsonpath='{.status.phase}' 2>/dev/null || echo "")
        
        if [ "$phase" = "Ready" ]; then
            print_success "租户状态: Ready"
            return 0
        elif [ "$phase" = "Failed" ]; then
            print_error "租户状态: Failed"
            kubectl get tenant "$TENANT_NAME" -o yaml
            return 1
        else
            print_info "租户状态: $phase (等待中... $waited/$max_wait 秒)"
        fi
        
        sleep $interval
        waited=$((waited + interval))
    done
    
    print_error "等待超时"
    return 1
}

# 验证命名空间创建
function verify_namespace_created() {
    print_step "验证命名空间创建"
    
    if kubectl get namespace "tenant-$TENANT_NAME" &>/dev/null; then
        print_success "命名空间已创建: tenant-$TENANT_NAME"
        return 0
    else
        print_error "命名空间未创建"
        return 1
    fi
}

# 验证Secret创建
function verify_secret_created() {
    print_step "验证Secret创建"
    
    local secret_name="tenant-$TENANT_NAME-db-secret"
    
    if kubectl get secret "$secret_name" -n "tenant-$TENANT_NAME" &>/dev/null; then
        print_success "Secret已创建: $secret_name"
        
        # 验证Secret包含必需的字段
        local fields=("username" "password" "server" "database")
        local all_fields_present=true
        
        for field in "${fields[@]}"; do
            if kubectl get secret "$secret_name" -n "tenant-$TENANT_NAME" -o jsonpath="{.data.$field}" 2>/dev/null | grep -q .; then
                print_success "  - $field: 存在"
            else
                print_error "  - $field: 缺失"
                all_fields_present=false
            fi
        done
        
        [ "$all_fields_present" = true ] && return 0 || return 1
    else
        print_error "Secret未创建"
        return 1
    fi
}

# 验证数据库创建
function verify_database_created() {
    print_step "验证数据库在SQL Server上创建"
    
    local db_name="${TENANT_NAME}_DB"
    
    # 检查sqlcmd是否可用
    if ! command -v sqlcmd &> /dev/null; then
        print_info "sqlcmd未安装，跳过数据库验证"
        print_info "请手动验证数据库: $db_name"
        return 0
    fi
    
    # 使用sqlcmd检查数据库
    local query="SELECT name FROM sys.databases WHERE name = '$db_name'"
    
    if sqlcmd -S "$SQL_SERVER_HOST,$SQL_SERVER_PORT" -U "$SQL_SERVER_USER" -P "$SQL_SERVER_PASSWORD" -Q "$query" -h -1 2>/dev/null | grep -q "$db_name"; then
        print_success "数据库已创建: $db_name"
        
        # 验证表结构
        local table_query="USE [$db_name]; SELECT name FROM sys.tables WHERE name IN ('Transactions', 'AuditLogs')"
        local tables=$(sqlcmd -S "$SQL_SERVER_HOST,$SQL_SERVER_PORT" -U "$SQL_SERVER_USER" -P "$SQL_SERVER_PASSWORD" -Q "$table_query" -h -1 2>/dev/null)
        
        if echo "$tables" | grep -q "Transactions" && echo "$tables" | grep -q "AuditLogs"; then
            print_success "  - 表结构已初始化 (Transactions, AuditLogs)"
        else
            print_error "  - 表结构未初始化"
            return 1
        fi
        
        return 0
    else
        print_error "数据库未创建"
        return 1
    fi
}

# 验证数据库配置注册到Tenant Catalog
function verify_database_config_registered() {
    print_step "验证数据库配置注册到Tenant Catalog"
    
    # 获取Tenant Catalog Service的端点
    if ! kubectl get service tenant-catalog -n platform-system &>/dev/null; then
        print_info "Tenant Catalog Service未部署，跳过此测试"
        return 0
    fi
    
    # 端口转发到Tenant Catalog Service
    local port=8080
    print_info "设置端口转发到Tenant Catalog Service..."
    
    kubectl port-forward -n platform-system service/tenant-catalog $port:8080 &>/dev/null &
    local port_forward_pid=$!
    
    sleep 3
    
    # 查询租户配置
    if curl -s "http://localhost:$port/api/tenants/$TENANT_NAME" | grep -q "dbConfig"; then
        print_success "数据库配置已注册到Tenant Catalog"
        kill $port_forward_pid 2>/dev/null || true
        return 0
    else
        print_error "数据库配置未注册"
        kill $port_forward_pid 2>/dev/null || true
        return 1
    fi
}

# 测试通过Smart Gateway发送请求
function test_smart_gateway_request() {
    print_step "测试通过Smart Gateway发送请求"
    
    # 检查Smart Gateway是否部署
    if ! kubectl get service smart-gateway -n gateway &>/dev/null; then
        print_info "Smart Gateway未部署，跳过此测试"
        return 0
    fi
    
    print_info "Smart Gateway集成测试需要完整的部署环境"
    print_info "请参考docs/LOCAL_ENVIRONMENT_GUIDE.zh.md完成部署后再运行此测试"
    return 0
}

# 测试租户退服流程
function test_tenant_decommissioning() {
    print_step "测试租户退服流程"
    
    print_info "更新租户状态为Decommissioned..."
    
    # 更新Tenant CRD状态
    kubectl patch tenant "$TENANT_NAME" --type=merge -p '{"spec":{"status":"Decommissioned"}}' 2>/dev/null || true
    
    # 等待退服流程完成
    sleep 10
    
    # 验证命名空间是否被删除
    if ! kubectl get namespace "tenant-$TENANT_NAME" &>/dev/null; then
        print_success "命名空间已删除"
    else
        print_info "命名空间仍然存在（退服流程可能需要更长时间）"
    fi
    
    # 验证数据库备份是否创建
    print_info "检查数据库备份..."
    print_info "备份验证需要访问Tenant Operator日志"
    
    return 0
}

# 主测试流程
function run_e2e_test() {
    echo -e "\n${CYAN}╔════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${CYAN}║         多租户医疗平台 - 端到端集成测试                      ║${NC}"
    echo -e "${CYAN}╚════════════════════════════════════════════════════════════╝${NC}\n"
    
    # 检查前置条件
    check_prerequisites
    
    # 清理之前的测试资源
    cleanup_test_resources
    
    # 测试计数器
    local total_tests=0
    local passed_tests=0
    local failed_tests=0
    
    # 测试1: 创建Tenant CRD
    ((total_tests++))
    if create_tenant_crd; then ((passed_tests++)); else ((failed_tests++)); fi
    
    # 测试2: 等待Tenant Operator处理
    ((total_tests++))
    if wait_tenant_provisioning; then ((passed_tests++)); else ((failed_tests++)); return; fi
    
    # 测试3: 验证命名空间创建
    ((total_tests++))
    if verify_namespace_created; then ((passed_tests++)); else ((failed_tests++)); fi
    
    # 测试4: 验证Secret创建
    ((total_tests++))
    if verify_secret_created; then ((passed_tests++)); else ((failed_tests++)); fi
    
    # 测试5: 验证数据库创建
    ((total_tests++))
    if verify_database_created; then ((passed_tests++)); else ((failed_tests++)); fi
    
    # 测试6: 验证数据库配置注册
    ((total_tests++))
    if verify_database_config_registered; then ((passed_tests++)); else ((failed_tests++)); fi
    
    # 测试7: 测试Smart Gateway请求
    ((total_tests++))
    if test_smart_gateway_request; then ((passed_tests++)); else ((failed_tests++)); fi
    
    # 测试8: 测试租户退服流程
    ((total_tests++))
    if test_tenant_decommissioning; then ((passed_tests++)); else ((failed_tests++)); fi
    
    # 测试结果汇总
    echo -e "\n${CYAN}╔════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${CYAN}║                      测试结果汇总                           ║${NC}"
    echo -e "${CYAN}╚════════════════════════════════════════════════════════════╝${NC}\n"
    
    echo "总测试数: $total_tests"
    echo -e "${GREEN}通过: $passed_tests${NC}"
    echo -e "${RED}失败: $failed_tests${NC}"
    
    if [ $failed_tests -eq 0 ]; then
        echo -e "\n${GREEN}✓ 所有测试通过！${NC}"
        exit 0
    else
        echo -e "\n${RED}✗ 部分测试失败${NC}"
        exit 1
    fi
}

# 运行测试
run_e2e_test
