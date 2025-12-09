package database

import (
	"context"
	"crypto/rand"
	"database/sql"
	"fmt"
	"regexp"
	"strings"

	_ "github.com/denisenkom/go-mssqldb" // SQL Server driver
	corev1 "k8s.io/api/core/v1"
	metav1 "k8s.io/apimachinery/pkg/apis/meta/v1"
	"sigs.k8s.io/controller-runtime/pkg/client"
	"sigs.k8s.io/controller-runtime/pkg/log"

	tenantsv1 "github.com/touchpoint-medical/tenant-operator/api/v1"
	"github.com/touchpoint-medical/tenant-operator/pkg/catalog"
)

// DatabaseProvisioner 负责为租户创建和配置数据库
type DatabaseProvisioner struct {
	sqlClient     *sql.DB        // SQL Server管理员连接
	catalogClient catalog.Client // Tenant Catalog客户端
	k8sClient     client.Client  // Kubernetes客户端
	sqlServerHost string         // SQL Server地址，如 "host.minikube.internal,1433"
}

// NewDatabaseProvisioner 创建新的DatabaseProvisioner实例
func NewDatabaseProvisioner(
	adminUser, adminPassword, sqlServerHost string,
	catalogClient catalog.Client,
	k8sClient client.Client,
) (*DatabaseProvisioner, error) {
	// 构建SQL Server连接字符串
	connString := fmt.Sprintf(
		"server=%s;user id=%s;password=%s;database=master;encrypt=disable",
		sqlServerHost, adminUser, adminPassword,
	)

	// 连接到SQL Server
	db, err := sql.Open("sqlserver", connString)
	if err != nil {
		return nil, fmt.Errorf("failed to open SQL Server connection: %w", err)
	}

	// 验证连接
	if err := db.Ping(); err != nil {
		db.Close()
		return nil, fmt.Errorf("failed to ping SQL Server: %w", err)
	}

	return &DatabaseProvisioner{
		sqlClient:     db,
		catalogClient: catalogClient,
		k8sClient:     k8sClient,
		sqlServerHost: sqlServerHost,
	}, nil
}

// Close 关闭数据库连接
func (p *DatabaseProvisioner) Close() error {
	if p.sqlClient != nil {
		return p.sqlClient.Close()
	}
	return nil
}

// ProvisionDatabase 为租户创建数据库、用户和初始化表结构
func (p *DatabaseProvisioner) ProvisionDatabase(ctx context.Context, tenant *tenantsv1.Tenant) error {
	logger := log.FromContext(ctx)
	logger.Info("Starting database provisioning", "tenant", tenant.Name)

	// 1. 生成数据库名称
	dbName := sanitizeName(tenant.Spec.DB.Database)
	if dbName == "" {
		dbName = fmt.Sprintf("%s_DB", sanitizeName(tenant.Spec.DisplayName))
	}

	// 2. 检查数据库是否已存在（幂等性）
	exists, err := p.databaseExists(ctx, dbName)
	if err != nil {
		return fmt.Errorf("failed to check if database exists: %w", err)
	}
	if exists {
		logger.Info("Database already exists, skipping creation", "database", dbName)
		// 数据库已存在，继续后续步骤（创建Secret和注册配置）
	} else {
		// 3. 生成数据库凭据
		username := fmt.Sprintf("%s_user", sanitizeName(tenant.Name))
		password, err := generateSecurePassword(32)
		if err != nil {
			return fmt.Errorf("failed to generate password: %w", err)
		}

		// 4. 在SQL Server上创建数据库
		if err := p.createDatabase(ctx, dbName); err != nil {
			return fmt.Errorf("failed to create database: %w", err)
		}
		logger.Info("Database created successfully", "database", dbName)

		// 5. 启用TDE加密（可选）
		if err := p.enableTDE(ctx, dbName); err != nil {
			// TDE失败不阻塞流程，记录警告
			logger.Info("Failed to enable TDE (optional feature)", "database", dbName, "error", err.Error())
		} else {
			logger.Info("TDE enabled successfully", "database", dbName)
		}

		// 6. 创建数据库用户和配置权限
		if err := p.createDatabaseUser(ctx, dbName, username, password); err != nil {
			// 回滚：删除数据库
			p.rollbackDatabase(ctx, dbName)
			return fmt.Errorf("failed to create database user: %w", err)
		}
		logger.Info("Database user created successfully", "username", username)

		// 7. 执行初始化脚本
		if err := p.executeInitScript(ctx, dbName); err != nil {
			// 回滚：删除数据库
			p.rollbackDatabase(ctx, dbName)
			return fmt.Errorf("failed to execute init script: %w", err)
		}
		logger.Info("Database initialization script executed successfully")

		// 8. 将凭据存储到Kubernetes Secret
		secretName := fmt.Sprintf("tenant-%s-db-secret", tenant.Name)
		namespaceName := fmt.Sprintf("tenant-%s", tenant.Name)
		if err := p.createK8sSecret(ctx, namespaceName, secretName, username, password, dbName); err != nil {
			// 回滚：删除数据库
			p.rollbackDatabase(ctx, dbName)
			return fmt.Errorf("failed to create Kubernetes secret: %w", err)
		}
		logger.Info("Kubernetes secret created successfully", "secret", secretName)
	}

	// 9. 将数据库配置注册到Tenant Catalog
	if p.catalogClient != nil {
		secretName := fmt.Sprintf("tenant-%s-db-secret", tenant.Name)
		username := fmt.Sprintf("%s_user", sanitizeName(tenant.Name))
		dbConfig := catalog.DatabaseConfig{
			Server:      p.sqlServerHost,
			Database:    dbName,
			Username:    username,
			PasswordRef: secretName,
		}
		if err := p.catalogClient.UpdateTenantDbConfig(ctx, tenant.Name, dbConfig); err != nil {
			logger.Error(err, "Failed to register database config to Tenant Catalog (non-fatal)")
			// 不回滚，因为数据库已创建成功
		} else {
			logger.Info("Database config registered to Tenant Catalog successfully")
		}
	}

	logger.Info("Database provisioning completed successfully", "tenant", tenant.Name, "database", dbName)
	return nil
}

// databaseExists 检查数据库是否已存在
func (p *DatabaseProvisioner) databaseExists(ctx context.Context, dbName string) (bool, error) {
	query := "SELECT COUNT(*) FROM sys.databases WHERE name = @p1"
	var count int
	err := p.sqlClient.QueryRowContext(ctx, query, dbName).Scan(&count)
	if err != nil {
		return false, err
	}
	return count > 0, nil
}

// createDatabase 创建数据库
func (p *DatabaseProvisioner) createDatabase(ctx context.Context, dbName string) error {
	query := fmt.Sprintf(`
		CREATE DATABASE [%s]
		COLLATE SQL_Latin1_General_CP1_CI_AS
	`, dbName)

	_, err := p.sqlClient.ExecContext(ctx, query)
	return err
}

// enableTDE 启用透明数据加密（可选功能）
func (p *DatabaseProvisioner) enableTDE(ctx context.Context, dbName string) error {
	// 检查TDE证书是否存在
	var certExists int
	err := p.sqlClient.QueryRowContext(ctx,
		"SELECT COUNT(*) FROM sys.certificates WHERE name = 'TDE_Cert'",
	).Scan(&certExists)
	if err != nil {
		return fmt.Errorf("failed to check TDE certificate: %w", err)
	}

	if certExists == 0 {
		return fmt.Errorf("TDE certificate 'TDE_Cert' not found, please create it first")
	}

	// 启用TDE
	query := fmt.Sprintf(`
		USE [%s];
		CREATE DATABASE ENCRYPTION KEY
		WITH ALGORITHM = AES_256
		ENCRYPTION BY SERVER CERTIFICATE TDE_Cert;
		ALTER DATABASE [%s] SET ENCRYPTION ON;
	`, dbName, dbName)

	_, err = p.sqlClient.ExecContext(ctx, query)
	return err
}

// createDatabaseUser 创建数据库用户并配置权限
func (p *DatabaseProvisioner) createDatabaseUser(ctx context.Context, dbName, username, password string) error {
	// 检查登录是否已存在
	var loginExists int
	err := p.sqlClient.QueryRowContext(ctx,
		"SELECT COUNT(*) FROM sys.server_principals WHERE name = @p1",
		username,
	).Scan(&loginExists)
	if err != nil {
		return fmt.Errorf("failed to check if login exists: %w", err)
	}

	// 创建登录（如果不存在）
	if loginExists == 0 {
		query := fmt.Sprintf(`
			USE master;
			CREATE LOGIN [%s] WITH PASSWORD = '%s';
		`, username, escapeSQLString(password))

		_, err = p.sqlClient.ExecContext(ctx, query)
		if err != nil {
			return fmt.Errorf("failed to create login: %w", err)
		}
	}

	// 创建数据库用户并授予权限
	query := fmt.Sprintf(`
		USE [%s];
		IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = '%s')
		BEGIN
			CREATE USER [%s] FOR LOGIN [%s];
		END
		ALTER ROLE db_owner ADD MEMBER [%s];
	`, dbName, username, username, username, username)

	_, err = p.sqlClient.ExecContext(ctx, query)
	if err != nil {
		return fmt.Errorf("failed to create user and grant permissions: %w", err)
	}

	return nil
}

// executeInitScript 执行数据库初始化脚本
func (p *DatabaseProvisioner) executeInitScript(ctx context.Context, dbName string) error {
	// 嵌入的初始化脚本
	initScript := fmt.Sprintf(`
		USE [%s];
		
		-- 创建Transactions表
		IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Transactions')
		BEGIN
			CREATE TABLE Transactions (
				Id BIGINT IDENTITY(1,1) PRIMARY KEY,
				Amount DECIMAL(18,2) NOT NULL,
				Description NVARCHAR(500),
				CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
			);
			CREATE INDEX IX_Transactions_CreatedAt ON Transactions(CreatedAt);
		END

		-- 创建AuditLogs表
		IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
		BEGIN
			CREATE TABLE AuditLogs (
				Id BIGINT IDENTITY(1,1) PRIMARY KEY,
				Action NVARCHAR(100) NOT NULL,
				UserId NVARCHAR(100),
				Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
				Details NVARCHAR(MAX)
			);
			CREATE INDEX IX_AuditLogs_Timestamp ON AuditLogs(Timestamp);
		END
	`, dbName)

	_, err := p.sqlClient.ExecContext(ctx, initScript)
	return err
}

// createK8sSecret 创建Kubernetes Secret存储数据库凭据
func (p *DatabaseProvisioner) createK8sSecret(ctx context.Context, namespace, secretName, username, password, dbName string) error {
	secret := &corev1.Secret{
		ObjectMeta: metav1.ObjectMeta{
			Name:      secretName,
			Namespace: namespace,
			Labels: map[string]string{
				"managedBy": "tenant-operator",
				"type":      "database-credentials",
			},
		},
		StringData: map[string]string{
			"username": username,
			"password": password,
			"server":   p.sqlServerHost,
			"database": dbName,
		},
		Type: corev1.SecretTypeOpaque,
	}

	return p.k8sClient.Create(ctx, secret)
}

// rollbackDatabase 回滚数据库创建（删除数据库）
func (p *DatabaseProvisioner) rollbackDatabase(ctx context.Context, dbName string) {
	logger := log.FromContext(ctx)
	logger.Info("Rolling back database creation", "database", dbName)

	query := fmt.Sprintf(`
		IF EXISTS (SELECT * FROM sys.databases WHERE name = '%s')
		BEGIN
			ALTER DATABASE [%s] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
			DROP DATABASE [%s];
		END
	`, dbName, dbName, dbName)

	_, err := p.sqlClient.ExecContext(ctx, query)
	if err != nil {
		logger.Error(err, "Failed to rollback database", "database", dbName)
	} else {
		logger.Info("Database rolled back successfully", "database", dbName)
	}
}

// generateSecurePassword 生成强密码
func generateSecurePassword(length int) (string, error) {
	const charset = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()"
	bytes := make([]byte, length)
	if _, err := rand.Read(bytes); err != nil {
		return "", err
	}

	// 将随机字节转换为charset中的字符
	for i := 0; i < length; i++ {
		bytes[i] = charset[int(bytes[i])%len(charset)]
	}

	return string(bytes), nil
}

// sanitizeName 清理名称用于数据库对象
func sanitizeName(name string) string {
	// 移除特殊字符，只保留字母数字和下划线
	reg := regexp.MustCompile("[^a-zA-Z0-9_]+")
	sanitized := reg.ReplaceAllString(name, "_")
	// 移除前导和尾随下划线
	sanitized = strings.Trim(sanitized, "_")
	return sanitized
}

// escapeSQLString 转义SQL字符串中的单引号
func escapeSQLString(s string) string {
	return strings.ReplaceAll(s, "'", "''")
}
