package main

import (
	"flag"
	"os"

	"k8s.io/apimachinery/pkg/runtime"
	utilruntime "k8s.io/apimachinery/pkg/util/runtime"
	clientgoscheme "k8s.io/client-go/kubernetes/scheme"
	ctrl "sigs.k8s.io/controller-runtime"
	"sigs.k8s.io/controller-runtime/pkg/healthz"
	"sigs.k8s.io/controller-runtime/pkg/log/zap"
	metricsserver "sigs.k8s.io/controller-runtime/pkg/metrics/server"
	"sigs.k8s.io/controller-runtime/pkg/webhook"

	tenantsv1 "github.com/touchpoint-medical/tenant-operator/api/v1"
	"github.com/touchpoint-medical/tenant-operator/controllers"
	"github.com/touchpoint-medical/tenant-operator/pkg/catalog"
	"github.com/touchpoint-medical/tenant-operator/pkg/database"
	"github.com/touchpoint-medical/tenant-operator/pkg/keyvault"
)

var (
	scheme   = runtime.NewScheme()
	setupLog = ctrl.Log.WithName("setup")
)

func init() {
	utilruntime.Must(clientgoscheme.AddToScheme(scheme))
	utilruntime.Must(tenantsv1.AddToScheme(scheme))
}

func main() {
	var metricsAddr string
	var enableLeaderElection bool
	var probeAddr string

	flag.StringVar(&metricsAddr, "metrics-bind-address", ":8080", "The address the metric endpoint binds to.")
	flag.StringVar(&probeAddr, "health-probe-bind-address", ":8081", "The address the probe endpoint binds to.")
	flag.BoolVar(&enableLeaderElection, "leader-elect", false,
		"Enable leader election for controller manager. "+
			"Enabling this will ensure there is only one active controller manager.")
	opts := zap.Options{
		Development: true,
	}
	opts.BindFlags(flag.CommandLine)
	flag.Parse()

	ctrl.SetLogger(zap.New(zap.UseFlagOptions(&opts)))

	mgr, err := ctrl.NewManager(ctrl.GetConfigOrDie(), ctrl.Options{
		Scheme:                  scheme,
		Metrics:                 metricsserver.Options{BindAddress: metricsAddr},
		WebhookServer:           webhook.NewServer(webhook.Options{Port: 9443}),
		HealthProbeBindAddress:  probeAddr,
		LeaderElection:          enableLeaderElection,
		LeaderElectionID:        "tenant-operator.medlogic.io",
		LeaderElectionNamespace: "platform-system",
	})
	if err != nil {
		setupLog.Error(err, "unable to start manager")
		os.Exit(1)
	}

	// Initialize Key Vault client (using mock for now)
	// In production, this would be replaced with actual Azure Key Vault client
	keyVaultClient := keyvault.NewMockClient()

	// Initialize Database Provisioner if SQL Server credentials are provided
	var dbProvisioner *database.DatabaseProvisioner
	sqlServerHost := os.Getenv("SQL_SERVER_HOST")
	sqlServerUser := os.Getenv("SQL_SERVER_ADMIN_USER")
	sqlServerPassword := os.Getenv("SQL_SERVER_ADMIN_PASSWORD")
	tenantCatalogURL := os.Getenv("TENANT_CATALOG_URL")

	if sqlServerHost != "" && sqlServerUser != "" && sqlServerPassword != "" {
		setupLog.Info("Initializing Database Provisioner",
			"sqlServerHost", sqlServerHost,
			"tenantCatalogURL", tenantCatalogURL)

		// Initialize Tenant Catalog client
		var catalogClient catalog.Client
		if tenantCatalogURL != "" {
			// 创建带有重试机制的 Catalog 客户端（最多重试 3 次）
			catalogClient = catalog.NewTenantCatalogClient(tenantCatalogURL, 3)
			setupLog.Info("Tenant Catalog client initialized", "baseURL", tenantCatalogURL)
		}

		// Initialize Database Provisioner
		var err error
		dbProvisioner, err = database.NewDatabaseProvisioner(
			sqlServerUser,
			sqlServerPassword,
			sqlServerHost,
			catalogClient,
			mgr.GetClient(),
		)
		if err != nil {
			setupLog.Error(err, "Failed to initialize Database Provisioner")
			// Don't exit, continue without database provisioning
			dbProvisioner = nil
		} else {
			setupLog.Info("Database Provisioner initialized successfully")
			// Ensure cleanup on shutdown
			defer func() {
				if dbProvisioner != nil {
					if err := dbProvisioner.Close(); err != nil {
						setupLog.Error(err, "Failed to close Database Provisioner")
					}
				}
			}()
		}
	} else {
		setupLog.Info("Database Provisioner not configured (missing SQL Server credentials)")
	}

	if err = (&controllers.TenantReconciler{
		Client:              mgr.GetClient(),
		Scheme:              mgr.GetScheme(),
		KeyVaultClient:      keyVaultClient,
		DatabaseProvisioner: dbProvisioner,
	}).SetupWithManager(mgr); err != nil {
		setupLog.Error(err, "unable to create controller", "controller", "Tenant")
		os.Exit(1)
	}

	if err := mgr.AddHealthzCheck("healthz", healthz.Ping); err != nil {
		setupLog.Error(err, "unable to set up health check")
		os.Exit(1)
	}
	if err := mgr.AddReadyzCheck("readyz", healthz.Ping); err != nil {
		setupLog.Error(err, "unable to set up ready check")
		os.Exit(1)
	}

	setupLog.Info("starting manager")
	if err := mgr.Start(ctrl.SetupSignalHandler()); err != nil {
		setupLog.Error(err, "problem running manager")
		os.Exit(1)
	}
}
