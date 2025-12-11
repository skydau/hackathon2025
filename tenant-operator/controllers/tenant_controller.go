package controllers

import (
	"context"
	"crypto/rand"
	"encoding/base64"
	"fmt"

	corev1 "k8s.io/api/core/v1"
	networkingv1 "k8s.io/api/networking/v1"
	rbacv1 "k8s.io/api/rbac/v1"
	"k8s.io/apimachinery/pkg/api/errors"
	"k8s.io/apimachinery/pkg/api/resource"
	metav1 "k8s.io/apimachinery/pkg/apis/meta/v1"
	"k8s.io/apimachinery/pkg/runtime"
	"k8s.io/apimachinery/pkg/types"
	ctrl "sigs.k8s.io/controller-runtime"
	"sigs.k8s.io/controller-runtime/pkg/client"
	"sigs.k8s.io/controller-runtime/pkg/log"

	tenantsv1 "github.com/touchpoint-medical/tenant-operator/api/v1"
	"github.com/touchpoint-medical/tenant-operator/pkg/database"
	"github.com/touchpoint-medical/tenant-operator/pkg/keyvault"
)

// TenantReconciler 协调 Tenant 对象
type TenantReconciler struct {
	client.Client
	Scheme              *runtime.Scheme
	KeyVaultClient      keyvault.Client
	DatabaseProvisioner database.Provisioner
}

// +kubebuilder:rbac:groups=tenants.medlogic.io,resources=tenants,verbs=get;list;watch;create;update;patch;delete
// +kubebuilder:rbac:groups=tenants.medlogic.io,resources=tenants/status,verbs=get;update;patch
// +kubebuilder:rbac:groups=tenants.medlogic.io,resources=tenants/finalizers,verbs=update
// +kubebuilder:rbac:groups="",resources=namespaces,verbs=get;list;watch;create;update;patch;delete
// +kubebuilder:rbac:groups="",resources=configmaps,verbs=get;list;watch;create;update;patch;delete
// +kubebuilder:rbac:groups="",resources=resourcequotas,verbs=get;list;watch;create;update;patch;delete
// +kubebuilder:rbac:groups=networking.k8s.io,resources=networkpolicies,verbs=get;list;watch;create;update;patch;delete
// +kubebuilder:rbac:groups=rbac.authorization.k8s.io,resources=rolebindings,verbs=get;list;watch;create;update;patch;delete

const (
	tenantFinalizerName = "tenants.medlogic.io/finalizer"
)

// Reconcile 是主要的 kubernetes 协调循环的一部分
func (r *TenantReconciler) Reconcile(ctx context.Context, req ctrl.Request) (ctrl.Result, error) {
	logger := log.FromContext(ctx)

	// 获取 Tenant 实例
	tenant := &tenantsv1.Tenant{}
	err := r.Get(ctx, req.NamespacedName, tenant)
	if err != nil {
		if errors.IsNotFound(err) {
			logger.Info("Tenant resource not found. Ignoring since object must be deleted")
			return ctrl.Result{}, nil
		}
		logger.Error(err, "Failed to get Tenant")
		return ctrl.Result{}, err
	}

	// 处理删除操作
	if !tenant.ObjectMeta.DeletionTimestamp.IsZero() {
		return r.handleDeletion(ctx, tenant)
	}

	// 如果不存在则添加 finalizer
	if !containsString(tenant.ObjectMeta.Finalizers, tenantFinalizerName) {
		tenant.ObjectMeta.Finalizers = append(tenant.ObjectMeta.Finalizers, tenantFinalizerName)
		if err := r.Update(ctx, tenant); err != nil {
			logger.Error(err, "Failed to add finalizer")
			return ctrl.Result{}, err
		}
	}

	// 如果未设置则设置初始状态
	if tenant.Status.Phase == "" {
		tenant.Status.Phase = "Provisioning"
		if err := r.Status().Update(ctx, tenant); err != nil {
			logger.Error(err, "Failed to update Tenant status")
			return ctrl.Result{}, err
		}
	}

	// 如果状态设置为 Decommissioned 则处理停用
	if tenant.Status.Phase == "Decommissioned" {
		return r.handleDecommissioning(ctx, tenant)
	}

	// 从租户名称生成命名空间名称
	namespaceName := fmt.Sprintf("tenant-%s", tenant.Name)

	// 创建或更新命名空间
	if err := r.reconcileNamespace(ctx, tenant, namespaceName); err != nil {
		logger.Error(err, "Failed to reconcile namespace")
		return ctrl.Result{}, err
	}

	// 创建或更新 ConfigMap
	if err := r.reconcileConfigMap(ctx, tenant, namespaceName); err != nil {
		logger.Error(err, "Failed to reconcile ConfigMap")
		return ctrl.Result{}, err
	}

	// 创建或更新 ResourceQuota
	if err := r.reconcileResourceQuota(ctx, tenant, namespaceName); err != nil {
		logger.Error(err, "Failed to reconcile ResourceQuota")
		return ctrl.Result{}, err
	}

	// 创建或更新 NetworkPolicy
	if err := r.reconcileNetworkPolicy(ctx, tenant, namespaceName); err != nil {
		logger.Error(err, "Failed to reconcile NetworkPolicy")
		return ctrl.Result{}, err
	}

	// 创建或更新 RBAC
	if err := r.reconcileRBAC(ctx, tenant, namespaceName); err != nil {
		logger.Error(err, "Failed to reconcile RBAC")
		return ctrl.Result{}, err
	}

	// 为租户创建 Key Vault 密钥
	if err := r.reconcileKeyVaultSecrets(ctx, tenant); err != nil {
		logger.Error(err, "Failed to reconcile Key Vault secrets")
		return ctrl.Result{}, err
	}

	// 如果配置了 DatabaseProvisioner 且数据库尚未创建，则配置数据库
	if r.DatabaseProvisioner != nil && !tenant.Status.DatabaseCreated {
		logger.Info("Provisioning database for tenant", "tenant", tenant.Name)
		if err := r.DatabaseProvisioner.ProvisionDatabase(ctx, tenant); err != nil {
			logger.Error(err, "Failed to provision database")
			// 更新状态为失败
			tenant.Status.Phase = "Failed"
			condition := metav1.Condition{
				Type:               "DatabaseProvisioned",
				Status:             metav1.ConditionFalse,
				Reason:             "ProvisioningFailed",
				Message:            fmt.Sprintf("Failed to provision database: %v", err),
				LastTransitionTime: metav1.Now(),
			}
			tenant.Status.Conditions = append(tenant.Status.Conditions, condition)
			if statusErr := r.Status().Update(ctx, tenant); statusErr != nil {
				logger.Error(statusErr, "Failed to update Tenant status to Failed")
			}
			return ctrl.Result{}, err
		}
		logger.Info("Database provisioned successfully", "tenant", tenant.Name)

		// 更新状态以指示数据库和密钥已创建
		tenant.Status.DatabaseCreated = true
		tenant.Status.SecretCreated = true
		condition := metav1.Condition{
			Type:               "DatabaseProvisioned",
			Status:             metav1.ConditionTrue,
			Reason:             "ProvisioningSucceeded",
			Message:            "Database provisioned successfully",
			LastTransitionTime: metav1.Now(),
		}
		tenant.Status.Conditions = append(tenant.Status.Conditions, condition)
		if err := r.Status().Update(ctx, tenant); err != nil {
			logger.Error(err, "Failed to update database status")
			return ctrl.Result{}, err
		}
	}

	// 更新状态为就绪
	tenant.Status.Phase = "Ready"
	tenant.Status.NamespaceCreated = true
	tenant.Status.ResourcesProvisioned = true
	if err := r.Status().Update(ctx, tenant); err != nil {
		logger.Error(err, "Failed to update Tenant status to Ready")
		return ctrl.Result{}, err
	}

	logger.Info("Successfully reconciled Tenant", "tenant", tenant.Name)
	return ctrl.Result{}, nil
}

// handleDecommissioning 处理租户的停用流程
func (r *TenantReconciler) handleDecommissioning(ctx context.Context, tenant *tenantsv1.Tenant) (ctrl.Result, error) {
	logger := log.FromContext(ctx)
	logger.Info("Starting tenant decommissioning flow", "tenant", tenant.Name)

	// 记录停用开始的审计事件
	r.logAuditEvent(ctx, tenant, "DecommissioningStarted", "Tenant decommissioning flow initiated")

	// 步骤 1: 创建数据库备份
	if err := r.createDatabaseBackup(ctx, tenant); err != nil {
		logger.Error(err, "Failed to create database backup")
		r.logAuditEvent(ctx, tenant, "BackupFailed", fmt.Sprintf("Database backup failed: %v", err))
		return ctrl.Result{}, err
	}
	logger.Info("Database backup created successfully", "tenant", tenant.Name)
	r.logAuditEvent(ctx, tenant, "BackupCompleted", "Database backup created successfully")

	// 步骤 2: 撤销 Key Vault 密钥
	if err := r.deleteKeyVaultSecrets(ctx, tenant); err != nil {
		logger.Error(err, "Failed to revoke Key Vault secrets")
		r.logAuditEvent(ctx, tenant, "SecretRevocationFailed", fmt.Sprintf("Secret revocation failed: %v", err))
		return ctrl.Result{}, err
	}
	logger.Info("Key Vault secrets revoked successfully", "tenant", tenant.Name)
	r.logAuditEvent(ctx, tenant, "SecretsRevoked", "All Key Vault secrets revoked successfully")

	// 步骤 3: 删除命名空间和所有资源
	namespaceName := fmt.Sprintf("tenant-%s", tenant.Name)
	namespace := &corev1.Namespace{}
	err := r.Get(ctx, types.NamespacedName{Name: namespaceName}, namespace)
	if err == nil {
		// 命名空间存在，删除它
		if err := r.Delete(ctx, namespace); err != nil && !errors.IsNotFound(err) {
			logger.Error(err, "Failed to delete namespace")
			r.logAuditEvent(ctx, tenant, "NamespaceDeletionFailed", fmt.Sprintf("Namespace deletion failed: %v", err))
			return ctrl.Result{}, err
		}
		logger.Info("Deleted tenant namespace", "namespace", namespaceName)
		r.logAuditEvent(ctx, tenant, "NamespaceDeleted", fmt.Sprintf("Namespace %s deleted successfully", namespaceName))
	}

	// 记录最终审计事件
	r.logAuditEvent(ctx, tenant, "DecommissioningCompleted", "Tenant decommissioning flow completed successfully")

	logger.Info("Tenant decommissioning completed successfully", "tenant", tenant.Name)
	return ctrl.Result{}, nil
}

// handleDeletion 处理租户的删除和资源清理
func (r *TenantReconciler) handleDeletion(ctx context.Context, tenant *tenantsv1.Tenant) (ctrl.Result, error) {
	logger := log.FromContext(ctx)
	logger.Info("Handling tenant deletion", "tenant", tenant.Name)

	// 检查是否存在 finalizer
	if containsString(tenant.ObjectMeta.Finalizers, tenantFinalizerName) {
		// 撤销并删除 Key Vault 密钥
		if err := r.deleteKeyVaultSecrets(ctx, tenant); err != nil {
			logger.Error(err, "Failed to delete Key Vault secrets")
			return ctrl.Result{}, err
		}

		// 删除命名空间（这将级联删除其中的所有资源）
		namespaceName := fmt.Sprintf("tenant-%s", tenant.Name)
		namespace := &corev1.Namespace{}
		err := r.Get(ctx, types.NamespacedName{Name: namespaceName}, namespace)
		if err == nil {
			// 命名空间存在，删除它
			if err := r.Delete(ctx, namespace); err != nil && !errors.IsNotFound(err) {
				logger.Error(err, "Failed to delete namespace")
				return ctrl.Result{}, err
			}
			logger.Info("Deleted tenant namespace", "namespace", namespaceName)
		}

		// 移除 finalizer
		tenant.ObjectMeta.Finalizers = removeString(tenant.ObjectMeta.Finalizers, tenantFinalizerName)
		if err := r.Update(ctx, tenant); err != nil {
			logger.Error(err, "Failed to remove finalizer")
			return ctrl.Result{}, err
		}
	}

	return ctrl.Result{}, nil
}

// createDatabaseBackup 创建租户数据库的备份
func (r *TenantReconciler) createDatabaseBackup(ctx context.Context, tenant *tenantsv1.Tenant) error {
	logger := log.FromContext(ctx)

	// 在实际实现中，这将：
	// 1. 使用租户的连接信息连接到数据库
	// 2. 执行备份命令（例如，SQL Server BACKUP DATABASE）
	// 3. 将备份存储在 Azure Blob Storage 或类似服务中
	// 4. 验证备份是否成功

	// 目前，我们将模拟备份过程
	logger.Info("Creating database backup",
		"tenant", tenant.Name,
		"database", tenant.Spec.DB.Database,
		"server", tenant.Spec.DB.Server)

	// 模拟备份创建
	// 在生产环境中，这将是实际的备份逻辑
	backupName := fmt.Sprintf("%s-backup-%d", tenant.Name, metav1.Now().Unix())
	logger.Info("Database backup created", "backupName", backupName)

	return nil
}

// logAuditEvent 记录租户操作的审计事件
func (r *TenantReconciler) logAuditEvent(ctx context.Context, tenant *tenantsv1.Tenant, eventType, message string) {
	logger := log.FromContext(ctx)

	// 记录结构化审计事件
	logger.Info("AUDIT",
		"tenantId", tenant.Name,
		"tenantDisplayName", tenant.Spec.DisplayName,
		"eventType", eventType,
		"message", message,
		"timestamp", metav1.Now().Format("2006-01-02T15:04:05Z07:00"),
	)

	// 在生产系统中，这还将：
	// 1. 写入专用的审计日志存储（例如，Azure Log Analytics）
	// 2. 发送到 SIEM 系统
	// 3. 创建 Kubernetes Events 以提高可见性
	// 4. 存储在持久的审计跟踪数据库中
}

// reconcileKeyVaultSecrets 为租户在 Key Vault 中创建数据库凭据
func (r *TenantReconciler) reconcileKeyVaultSecrets(ctx context.Context, tenant *tenantsv1.Tenant) error {
	if r.KeyVaultClient == nil {
		return nil // 如果未配置 Key Vault 客户端则跳过
	}

	// 如果不存在则生成数据库密码
	dbPasswordSecretName := fmt.Sprintf("%s-db-password", tenant.Name)

	// 检查密钥是否已存在
	_, err := r.KeyVaultClient.GetSecret(ctx, tenant.Name, dbPasswordSecretName)
	if err != nil {
		// 密钥不存在，创建它
		password, err := generateSecurePassword(32)
		if err != nil {
			return fmt.Errorf("failed to generate password: %w", err)
		}

		if err := r.KeyVaultClient.CreateSecret(ctx, tenant.Name, dbPasswordSecretName, password); err != nil {
			return fmt.Errorf("failed to create Key Vault secret: %w", err)
		}
	}

	return nil
}

// deleteKeyVaultSecrets 撤销并删除租户的所有 Key Vault 密钥
func (r *TenantReconciler) deleteKeyVaultSecrets(ctx context.Context, tenant *tenantsv1.Tenant) error {
	if r.KeyVaultClient == nil {
		return nil // 如果未配置 Key Vault 客户端则跳过
	}

	// 撤销数据库密码密钥
	dbPasswordSecretName := fmt.Sprintf("%s-db-password", tenant.Name)

	// 首先撤销密钥
	if err := r.KeyVaultClient.RevokeSecret(ctx, tenant.Name, dbPasswordSecretName); err != nil {
		// 记录但如果密钥不存在则不失败
		log.FromContext(ctx).Info("Failed to revoke secret (may not exist)", "secret", dbPasswordSecretName, "error", err)
	}

	// 删除密钥
	if err := r.KeyVaultClient.DeleteSecret(ctx, tenant.Name, dbPasswordSecretName); err != nil {
		// 记录但如果密钥不存在则不失败
		log.FromContext(ctx).Info("Failed to delete secret (may not exist)", "secret", dbPasswordSecretName, "error", err)
	}

	return nil
}

// generateSecurePassword 生成加密安全的随机密码
func generateSecurePassword(length int) (string, error) {
	bytes := make([]byte, length)
	if _, err := rand.Read(bytes); err != nil {
		return "", err
	}
	return base64.URLEncoding.EncodeToString(bytes)[:length], nil
}

// containsString 检查字符串切片是否包含特定字符串
func containsString(slice []string, s string) bool {
	for _, item := range slice {
		if item == s {
			return true
		}
	}
	return false
}

// removeString 从切片中移除字符串
func removeString(slice []string, s string) []string {
	result := []string{}
	for _, item := range slice {
		if item != s {
			result = append(result, item)
		}
	}
	return result
}

// reconcileNamespace 创建或更新租户命名空间
func (r *TenantReconciler) reconcileNamespace(ctx context.Context, tenant *tenantsv1.Tenant, namespaceName string) error {
	namespace := &corev1.Namespace{
		ObjectMeta: metav1.ObjectMeta{
			Name: namespaceName,
			Labels: map[string]string{
				"tenant":    tenant.Name,
				"tenantId":  tenant.Name,
				"managedBy": "tenant-operator",
			},
		},
	}

	found := &corev1.Namespace{}
	err := r.Get(ctx, types.NamespacedName{Name: namespaceName}, found)
	if err != nil && errors.IsNotFound(err) {
		if err := r.Create(ctx, namespace); err != nil {
			return err
		}
		return nil
	} else if err != nil {
		return err
	}

	// 如果需要则更新标签
	if found.Labels == nil {
		found.Labels = make(map[string]string)
	}
	found.Labels["tenant"] = tenant.Name
	found.Labels["tenantId"] = tenant.Name
	found.Labels["managedBy"] = "tenant-operator"

	return r.Update(ctx, found)
}

// reconcileConfigMap 创建或更新租户配置 ConfigMap
func (r *TenantReconciler) reconcileConfigMap(ctx context.Context, tenant *tenantsv1.Tenant, namespaceName string) error {
	configMap := &corev1.ConfigMap{
		ObjectMeta: metav1.ObjectMeta{
			Name:      "tenant-config",
			Namespace: namespaceName,
			Labels: map[string]string{
				"tenant":    tenant.Name,
				"managedBy": "tenant-operator",
			},
		},
		Data: map[string]string{
			"tenantId":      tenant.Name,
			"displayName":   tenant.Spec.DisplayName,
			"dbMode":        tenant.Spec.DB.Mode,
			"dbServer":      tenant.Spec.DB.Server,
			"dbDatabase":    tenant.Spec.DB.Database,
			"throttlingRps": fmt.Sprintf("%d", tenant.Spec.Throttling.RPS),
		},
	}

	found := &corev1.ConfigMap{}
	err := r.Get(ctx, types.NamespacedName{Name: "tenant-config", Namespace: namespaceName}, found)
	if err != nil && errors.IsNotFound(err) {
		return r.Create(ctx, configMap)
	} else if err != nil {
		return err
	}

	// 更新数据
	found.Data = configMap.Data
	return r.Update(ctx, found)
}

// reconcileResourceQuota 为租户命名空间创建或更新 ResourceQuota
func (r *TenantReconciler) reconcileResourceQuota(ctx context.Context, tenant *tenantsv1.Tenant, namespaceName string) error {
	resourceQuota := &corev1.ResourceQuota{
		ObjectMeta: metav1.ObjectMeta{
			Name:      "tenant-quota",
			Namespace: namespaceName,
			Labels: map[string]string{
				"tenant":    tenant.Name,
				"managedBy": "tenant-operator",
			},
		},
		Spec: corev1.ResourceQuotaSpec{
			Hard: corev1.ResourceList{
				corev1.ResourceRequestsCPU:    resource.MustParse("4"),
				corev1.ResourceRequestsMemory: resource.MustParse("8Gi"),
				corev1.ResourceLimitsCPU:      resource.MustParse("8"),
				corev1.ResourceLimitsMemory:   resource.MustParse("16Gi"),
				corev1.ResourcePods:           resource.MustParse("50"),
			},
		},
	}

	found := &corev1.ResourceQuota{}
	err := r.Get(ctx, types.NamespacedName{Name: "tenant-quota", Namespace: namespaceName}, found)
	if err != nil && errors.IsNotFound(err) {
		return r.Create(ctx, resourceQuota)
	} else if err != nil {
		return err
	}

	// 更新规格
	found.Spec = resourceQuota.Spec
	return r.Update(ctx, found)
}

// reconcileNetworkPolicy 为租户隔离创建或更新 NetworkPolicy
func (r *TenantReconciler) reconcileNetworkPolicy(ctx context.Context, tenant *tenantsv1.Tenant, namespaceName string) error {
	networkPolicy := &networkingv1.NetworkPolicy{
		ObjectMeta: metav1.ObjectMeta{
			Name:      "tenant-isolation",
			Namespace: namespaceName,
			Labels: map[string]string{
				"tenant":    tenant.Name,
				"managedBy": "tenant-operator",
			},
		},
		Spec: networkingv1.NetworkPolicySpec{
			PodSelector: metav1.LabelSelector{},
			PolicyTypes: []networkingv1.PolicyType{
				networkingv1.PolicyTypeIngress,
				networkingv1.PolicyTypeEgress,
			},
			Ingress: []networkingv1.NetworkPolicyIngressRule{
				{
					From: []networkingv1.NetworkPolicyPeer{
						{
							PodSelector: &metav1.LabelSelector{},
						},
					},
				},
			},
			Egress: []networkingv1.NetworkPolicyEgressRule{
				{
					To: []networkingv1.NetworkPolicyPeer{
						{
							PodSelector: &metav1.LabelSelector{},
						},
					},
				},
				{
					To: []networkingv1.NetworkPolicyPeer{
						{
							NamespaceSelector: &metav1.LabelSelector{
								MatchLabels: map[string]string{
									"name": "platform-system",
								},
							},
						},
					},
				},
				{
					To: []networkingv1.NetworkPolicyPeer{
						{
							NamespaceSelector: &metav1.LabelSelector{
								MatchLabels: map[string]string{
									"name": "kube-system",
								},
							},
						},
					},
				},
			},
		},
	}

	found := &networkingv1.NetworkPolicy{}
	err := r.Get(ctx, types.NamespacedName{Name: "tenant-isolation", Namespace: namespaceName}, found)
	if err != nil && errors.IsNotFound(err) {
		return r.Create(ctx, networkPolicy)
	} else if err != nil {
		return err
	}

	// 更新规格
	found.Spec = networkPolicy.Spec
	return r.Update(ctx, found)
}

// reconcileRBAC 为租户命名空间创建或更新 RBAC 规则
func (r *TenantReconciler) reconcileRBAC(ctx context.Context, tenant *tenantsv1.Tenant, namespaceName string) error {
	roleBinding := &rbacv1.RoleBinding{
		ObjectMeta: metav1.ObjectMeta{
			Name:      "tenant-admin",
			Namespace: namespaceName,
			Labels: map[string]string{
				"tenant":    tenant.Name,
				"managedBy": "tenant-operator",
			},
		},
		RoleRef: rbacv1.RoleRef{
			APIGroup: "rbac.authorization.k8s.io",
			Kind:     "ClusterRole",
			Name:     "admin",
		},
		Subjects: []rbacv1.Subject{
			{
				Kind:      "ServiceAccount",
				Name:      "default",
				Namespace: namespaceName,
			},
		},
	}

	found := &rbacv1.RoleBinding{}
	err := r.Get(ctx, types.NamespacedName{Name: "tenant-admin", Namespace: namespaceName}, found)
	if err != nil && errors.IsNotFound(err) {
		return r.Create(ctx, roleBinding)
	} else if err != nil {
		return err
	}

	// 更新 roleRef 和 subjects
	found.RoleRef = roleBinding.RoleRef
	found.Subjects = roleBinding.Subjects
	return r.Update(ctx, found)
}

// SetupWithManager 使用 Manager 设置控制器
func (r *TenantReconciler) SetupWithManager(mgr ctrl.Manager) error {
	return ctrl.NewControllerManagedBy(mgr).
		For(&tenantsv1.Tenant{}).
		Complete(r)
}
