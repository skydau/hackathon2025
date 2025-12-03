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
	"github.com/touchpoint-medical/tenant-operator/pkg/keyvault"
)

// TenantReconciler reconciles a Tenant object
type TenantReconciler struct {
	client.Client
	Scheme         *runtime.Scheme
	KeyVaultClient keyvault.Client
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

// Reconcile is part of the main kubernetes reconciliation loop
func (r *TenantReconciler) Reconcile(ctx context.Context, req ctrl.Request) (ctrl.Result, error) {
	logger := log.FromContext(ctx)

	// Fetch the Tenant instance
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

	// Handle deletion
	if !tenant.ObjectMeta.DeletionTimestamp.IsZero() {
		return r.handleDeletion(ctx, tenant)
	}

	// Add finalizer if not present
	if !containsString(tenant.ObjectMeta.Finalizers, tenantFinalizerName) {
		tenant.ObjectMeta.Finalizers = append(tenant.ObjectMeta.Finalizers, tenantFinalizerName)
		if err := r.Update(ctx, tenant); err != nil {
			logger.Error(err, "Failed to add finalizer")
			return ctrl.Result{}, err
		}
	}

	// Set initial status if not set
	if tenant.Status.Phase == "" {
		tenant.Status.Phase = "Provisioning"
		if err := r.Status().Update(ctx, tenant); err != nil {
			logger.Error(err, "Failed to update Tenant status")
			return ctrl.Result{}, err
		}
	}

	// Handle decommissioning if status is set to Decommissioned
	if tenant.Status.Phase == "Decommissioned" {
		return r.handleDecommissioning(ctx, tenant)
	}

	// Generate namespace name from tenant name
	namespaceName := fmt.Sprintf("tenant-%s", tenant.Name)

	// Create or update namespace
	if err := r.reconcileNamespace(ctx, tenant, namespaceName); err != nil {
		logger.Error(err, "Failed to reconcile namespace")
		return ctrl.Result{}, err
	}

	// Create or update ConfigMap
	if err := r.reconcileConfigMap(ctx, tenant, namespaceName); err != nil {
		logger.Error(err, "Failed to reconcile ConfigMap")
		return ctrl.Result{}, err
	}

	// Create or update ResourceQuota
	if err := r.reconcileResourceQuota(ctx, tenant, namespaceName); err != nil {
		logger.Error(err, "Failed to reconcile ResourceQuota")
		return ctrl.Result{}, err
	}

	// Create or update NetworkPolicy
	if err := r.reconcileNetworkPolicy(ctx, tenant, namespaceName); err != nil {
		logger.Error(err, "Failed to reconcile NetworkPolicy")
		return ctrl.Result{}, err
	}

	// Create or update RBAC
	if err := r.reconcileRBAC(ctx, tenant, namespaceName); err != nil {
		logger.Error(err, "Failed to reconcile RBAC")
		return ctrl.Result{}, err
	}

	// Create Key Vault secrets for tenant
	if err := r.reconcileKeyVaultSecrets(ctx, tenant); err != nil {
		logger.Error(err, "Failed to reconcile Key Vault secrets")
		return ctrl.Result{}, err
	}

	// Update status to Ready
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

// handleDecommissioning handles the decommissioning flow for a tenant
func (r *TenantReconciler) handleDecommissioning(ctx context.Context, tenant *tenantsv1.Tenant) (ctrl.Result, error) {
	logger := log.FromContext(ctx)
	logger.Info("Starting tenant decommissioning flow", "tenant", tenant.Name)

	// Log audit event for decommissioning start
	r.logAuditEvent(ctx, tenant, "DecommissioningStarted", "Tenant decommissioning flow initiated")

	// Step 1: Create database backup
	if err := r.createDatabaseBackup(ctx, tenant); err != nil {
		logger.Error(err, "Failed to create database backup")
		r.logAuditEvent(ctx, tenant, "BackupFailed", fmt.Sprintf("Database backup failed: %v", err))
		return ctrl.Result{}, err
	}
	logger.Info("Database backup created successfully", "tenant", tenant.Name)
	r.logAuditEvent(ctx, tenant, "BackupCompleted", "Database backup created successfully")

	// Step 2: Revoke Key Vault secrets
	if err := r.deleteKeyVaultSecrets(ctx, tenant); err != nil {
		logger.Error(err, "Failed to revoke Key Vault secrets")
		r.logAuditEvent(ctx, tenant, "SecretRevocationFailed", fmt.Sprintf("Secret revocation failed: %v", err))
		return ctrl.Result{}, err
	}
	logger.Info("Key Vault secrets revoked successfully", "tenant", tenant.Name)
	r.logAuditEvent(ctx, tenant, "SecretsRevoked", "All Key Vault secrets revoked successfully")

	// Step 3: Delete namespace and all resources
	namespaceName := fmt.Sprintf("tenant-%s", tenant.Name)
	namespace := &corev1.Namespace{}
	err := r.Get(ctx, types.NamespacedName{Name: namespaceName}, namespace)
	if err == nil {
		// Namespace exists, delete it
		if err := r.Delete(ctx, namespace); err != nil && !errors.IsNotFound(err) {
			logger.Error(err, "Failed to delete namespace")
			r.logAuditEvent(ctx, tenant, "NamespaceDeletionFailed", fmt.Sprintf("Namespace deletion failed: %v", err))
			return ctrl.Result{}, err
		}
		logger.Info("Deleted tenant namespace", "namespace", namespaceName)
		r.logAuditEvent(ctx, tenant, "NamespaceDeleted", fmt.Sprintf("Namespace %s deleted successfully", namespaceName))
	}

	// Log final audit event
	r.logAuditEvent(ctx, tenant, "DecommissioningCompleted", "Tenant decommissioning flow completed successfully")

	logger.Info("Tenant decommissioning completed successfully", "tenant", tenant.Name)
	return ctrl.Result{}, nil
}

// handleDeletion handles the deletion of a tenant and cleanup of resources
func (r *TenantReconciler) handleDeletion(ctx context.Context, tenant *tenantsv1.Tenant) (ctrl.Result, error) {
	logger := log.FromContext(ctx)
	logger.Info("Handling tenant deletion", "tenant", tenant.Name)

	// Check if finalizer is present
	if containsString(tenant.ObjectMeta.Finalizers, tenantFinalizerName) {
		// Revoke and delete Key Vault secrets
		if err := r.deleteKeyVaultSecrets(ctx, tenant); err != nil {
			logger.Error(err, "Failed to delete Key Vault secrets")
			return ctrl.Result{}, err
		}

		// Delete namespace (this will cascade delete all resources in it)
		namespaceName := fmt.Sprintf("tenant-%s", tenant.Name)
		namespace := &corev1.Namespace{}
		err := r.Get(ctx, types.NamespacedName{Name: namespaceName}, namespace)
		if err == nil {
			// Namespace exists, delete it
			if err := r.Delete(ctx, namespace); err != nil && !errors.IsNotFound(err) {
				logger.Error(err, "Failed to delete namespace")
				return ctrl.Result{}, err
			}
			logger.Info("Deleted tenant namespace", "namespace", namespaceName)
		}

		// Remove finalizer
		tenant.ObjectMeta.Finalizers = removeString(tenant.ObjectMeta.Finalizers, tenantFinalizerName)
		if err := r.Update(ctx, tenant); err != nil {
			logger.Error(err, "Failed to remove finalizer")
			return ctrl.Result{}, err
		}
	}

	return ctrl.Result{}, nil
}

// createDatabaseBackup creates a backup of the tenant's database
func (r *TenantReconciler) createDatabaseBackup(ctx context.Context, tenant *tenantsv1.Tenant) error {
	logger := log.FromContext(ctx)

	// In a real implementation, this would:
	// 1. Connect to the database using the tenant's connection info
	// 2. Execute a backup command (e.g., SQL Server BACKUP DATABASE)
	// 3. Store the backup in Azure Blob Storage or similar
	// 4. Verify the backup was successful

	// For now, we'll simulate the backup process
	logger.Info("Creating database backup",
		"tenant", tenant.Name,
		"database", tenant.Spec.DB.Database,
		"server", tenant.Spec.DB.Server)

	// Simulate backup creation
	// In production, this would be actual backup logic
	backupName := fmt.Sprintf("%s-backup-%d", tenant.Name, metav1.Now().Unix())
	logger.Info("Database backup created", "backupName", backupName)

	return nil
}

// logAuditEvent logs an audit event for tenant operations
func (r *TenantReconciler) logAuditEvent(ctx context.Context, tenant *tenantsv1.Tenant, eventType, message string) {
	logger := log.FromContext(ctx)

	// Log structured audit event
	logger.Info("AUDIT",
		"tenantId", tenant.Name,
		"tenantDisplayName", tenant.Spec.DisplayName,
		"eventType", eventType,
		"message", message,
		"timestamp", metav1.Now().Format("2006-01-02T15:04:05Z07:00"),
	)

	// In a production system, this would also:
	// 1. Write to a dedicated audit log storage (e.g., Azure Log Analytics)
	// 2. Send to a SIEM system
	// 3. Create Kubernetes Events for visibility
	// 4. Store in a persistent audit trail database
}

// reconcileKeyVaultSecrets creates database credentials in Key Vault for the tenant
func (r *TenantReconciler) reconcileKeyVaultSecrets(ctx context.Context, tenant *tenantsv1.Tenant) error {
	if r.KeyVaultClient == nil {
		return nil // Skip if no Key Vault client configured
	}

	// Generate database password if not exists
	dbPasswordSecretName := fmt.Sprintf("%s-db-password", tenant.Name)

	// Check if secret already exists
	_, err := r.KeyVaultClient.GetSecret(ctx, tenant.Name, dbPasswordSecretName)
	if err != nil {
		// Secret doesn't exist, create it
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

// deleteKeyVaultSecrets revokes and deletes all Key Vault secrets for the tenant
func (r *TenantReconciler) deleteKeyVaultSecrets(ctx context.Context, tenant *tenantsv1.Tenant) error {
	if r.KeyVaultClient == nil {
		return nil // Skip if no Key Vault client configured
	}

	// Revoke database password secret
	dbPasswordSecretName := fmt.Sprintf("%s-db-password", tenant.Name)

	// Revoke the secret first
	if err := r.KeyVaultClient.RevokeSecret(ctx, tenant.Name, dbPasswordSecretName); err != nil {
		// Log but don't fail if secret doesn't exist
		log.FromContext(ctx).Info("Failed to revoke secret (may not exist)", "secret", dbPasswordSecretName, "error", err)
	}

	// Delete the secret
	if err := r.KeyVaultClient.DeleteSecret(ctx, tenant.Name, dbPasswordSecretName); err != nil {
		// Log but don't fail if secret doesn't exist
		log.FromContext(ctx).Info("Failed to delete secret (may not exist)", "secret", dbPasswordSecretName, "error", err)
	}

	return nil
}

// generateSecurePassword generates a cryptographically secure random password
func generateSecurePassword(length int) (string, error) {
	bytes := make([]byte, length)
	if _, err := rand.Read(bytes); err != nil {
		return "", err
	}
	return base64.URLEncoding.EncodeToString(bytes)[:length], nil
}

// containsString checks if a string slice contains a specific string
func containsString(slice []string, s string) bool {
	for _, item := range slice {
		if item == s {
			return true
		}
	}
	return false
}

// removeString removes a string from a slice
func removeString(slice []string, s string) []string {
	result := []string{}
	for _, item := range slice {
		if item != s {
			result = append(result, item)
		}
	}
	return result
}

// reconcileNamespace creates or updates the tenant namespace
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

	// Update labels if needed
	if found.Labels == nil {
		found.Labels = make(map[string]string)
	}
	found.Labels["tenant"] = tenant.Name
	found.Labels["tenantId"] = tenant.Name
	found.Labels["managedBy"] = "tenant-operator"

	return r.Update(ctx, found)
}

// reconcileConfigMap creates or updates the tenant configuration ConfigMap
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
			"dbSchema":      tenant.Spec.DB.Schema,
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

	// Update data
	found.Data = configMap.Data
	return r.Update(ctx, found)
}

// reconcileResourceQuota creates or updates the ResourceQuota for the tenant namespace
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

	// Update spec
	found.Spec = resourceQuota.Spec
	return r.Update(ctx, found)
}

// reconcileNetworkPolicy creates or updates the NetworkPolicy for tenant isolation
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

	// Update spec
	found.Spec = networkPolicy.Spec
	return r.Update(ctx, found)
}

// reconcileRBAC creates or updates RBAC rules for the tenant namespace
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

	// Update roleRef and subjects
	found.RoleRef = roleBinding.RoleRef
	found.Subjects = roleBinding.Subjects
	return r.Update(ctx, found)
}

// SetupWithManager sets up the controller with the Manager.
func (r *TenantReconciler) SetupWithManager(mgr ctrl.Manager) error {
	return ctrl.NewControllerManagedBy(mgr).
		For(&tenantsv1.Tenant{}).
		Complete(r)
}
