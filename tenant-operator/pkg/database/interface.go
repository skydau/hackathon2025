package database

import (
	"context"

	tenantsv1 "github.com/touchpoint-medical/tenant-operator/api/v1"
)

// Provisioner 定义数据库provisioner的接口
type Provisioner interface {
	ProvisionDatabase(ctx context.Context, tenant *tenantsv1.Tenant) error
	Close() error
}
