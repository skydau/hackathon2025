package medlogic

import (
	"context"
	"database/sql"
	"fmt"
	"os"
	"time"

	_ "github.com/denisenkom/go-mssqldb"
	"github.com/go-logr/logr"
	tenantsv1 "github.com/touchpoint-medical/tenant-operator/api/v1"
)

// TenantStatus represents the status enum in MedLogicPlatform database
type TenantStatus int

const (
	Provisioning TenantStatus = 0
	Enabled      TenantStatus = 1
	Disabled     TenantStatus = 2
	Decommissioned TenantStatus = 3
)

// MedLogicSyncer handles synchronization with MedLogicPlatform database
type MedLogicSyncer struct {
	db     *sql.DB
	logger logr.Logger
}

// NewMedLogicSyncer creates a new MedLogicPlatform database syncer
func NewMedLogicSyncer(logger logr.Logger) (*MedLogicSyncer, error) {
	host := os.Getenv("MEDLOGIC_DB_HOST")
	dbName := os.Getenv("MEDLOGIC_DB_NAME")
	user := os.Getenv("MEDLOGIC_DB_USER")
	password := os.Getenv("MEDLOGIC_DB_PASSWORD")

	if host == "" || dbName == "" || user == "" || password == "" {
		return nil, fmt.Errorf("missing required MedLogicPlatform database configuration")
	}

	connString := fmt.Sprintf("server=%s;user id=%s;password=%s;database=%s;encrypt=false", 
		host, user, password, dbName)

	db, err := sql.Open("sqlserver", connString)
	if err != nil {
		return nil, fmt.Errorf("failed to connect to MedLogicPlatform database: %w", err)
	}

	// Test connection
	if err := db.Ping(); err != nil {
		return nil, fmt.Errorf("failed to ping MedLogicPlatform database: %w", err)
	}

	logger.Info("Successfully connected to MedLogicPlatform database", "host", host, "database", dbName)

	return &MedLogicSyncer{
		db:     db,
		logger: logger,
	}, nil
}

// SyncTenant synchronizes tenant information to MedLogicPlatform database
func (s *MedLogicSyncer) SyncTenant(ctx context.Context, tenant *tenantsv1.Tenant) error {
	logger := s.logger.WithValues("tenant", tenant.Name)
	logger.Info("Syncing tenant to MedLogicPlatform database")

	// Convert Kubernetes phase to MedLogicPlatform status
	var status TenantStatus
	switch tenant.Status.Phase {
	case "Provisioning":
		status = Provisioning
	case "Ready":
		status = Enabled
	case "Failed":
		status = Disabled
	case "Decommissioned":
		status = Decommissioned
	default:
		status = Provisioning
	}

	// Check if tenant already exists
	var existingId string
	checkQuery := "SELECT Id FROM Tenants WHERE Id = ?"
	err := s.db.QueryRowContext(ctx, checkQuery, tenant.Name).Scan(&existingId)
	
	now := time.Now()
	
	if err == sql.ErrNoRows {
		// Insert new tenant
		insertQuery := `
			INSERT INTO Tenants (Id, DisplayName, Status, CreatedAt, UpdatedAt)
			VALUES (?, ?, ?, ?, ?)
		`
		_, err = s.db.ExecContext(ctx, insertQuery, 
			tenant.Name, 
			tenant.Spec.DisplayName, 
			int(status), 
			now, 
			now)
		
		if err != nil {
			return fmt.Errorf("failed to insert tenant into MedLogicPlatform database: %w", err)
		}
		
		logger.Info("Successfully inserted tenant into MedLogicPlatform database")
	} else if err != nil {
		return fmt.Errorf("failed to check existing tenant: %w", err)
	} else {
		// Update existing tenant
		updateQuery := `
			UPDATE Tenants 
			SET DisplayName = ?, Status = ?, UpdatedAt = ?
			WHERE Id = ?
		`
		_, err = s.db.ExecContext(ctx, updateQuery, 
			tenant.Spec.DisplayName, 
			int(status), 
			now, 
			tenant.Name)
		
		if err != nil {
			return fmt.Errorf("failed to update tenant in MedLogicPlatform database: %w", err)
		}
		
		logger.Info("Successfully updated tenant in MedLogicPlatform database")
	}

	return nil
}

// DeleteTenant removes tenant from MedLogicPlatform database
func (s *MedLogicSyncer) DeleteTenant(ctx context.Context, tenantName string) error {
	logger := s.logger.WithValues("tenant", tenantName)
	logger.Info("Deleting tenant from MedLogicPlatform database")

	deleteQuery := "DELETE FROM Tenants WHERE Id = ?"
	result, err := s.db.ExecContext(ctx, deleteQuery, tenantName)
	if err != nil {
		return fmt.Errorf("failed to delete tenant from MedLogicPlatform database: %w", err)
	}

	rowsAffected, err := result.RowsAffected()
	if err != nil {
		return fmt.Errorf("failed to get rows affected: %w", err)
	}

	if rowsAffected == 0 {
		logger.Info("Tenant not found in MedLogicPlatform database, nothing to delete")
	} else {
		logger.Info("Successfully deleted tenant from MedLogicPlatform database")
	}

	return nil
}

// Close closes the database connection
func (s *MedLogicSyncer) Close() error {
	if s.db != nil {
		return s.db.Close()
	}
	return nil
}