package keyvault

import (
	"context"
	"fmt"
	"sync"
)

// Client is an interface for Azure Key Vault operations
type Client interface {
	// CreateSecret creates a new secret in Key Vault
	CreateSecret(ctx context.Context, tenantID, secretName, secretValue string) error

	// GetSecret retrieves a secret from Key Vault
	GetSecret(ctx context.Context, tenantID, secretName string) (string, error)

	// DeleteSecret deletes a secret from Key Vault
	DeleteSecret(ctx context.Context, tenantID, secretName string) error

	// RevokeSecret revokes access to a secret (marks it as disabled)
	RevokeSecret(ctx context.Context, tenantID, secretName string) error
}

// MockClient is a mock implementation of the Key Vault client for testing
type MockClient struct {
	mu      sync.RWMutex
	secrets map[string]map[string]string // tenantID -> secretName -> secretValue
}

// NewMockClient creates a new mock Key Vault client
func NewMockClient() *MockClient {
	return &MockClient{
		secrets: make(map[string]map[string]string),
	}
}

// CreateSecret creates a new secret in the mock Key Vault
func (m *MockClient) CreateSecret(ctx context.Context, tenantID, secretName, secretValue string) error {
	m.mu.Lock()
	defer m.mu.Unlock()

	if m.secrets[tenantID] == nil {
		m.secrets[tenantID] = make(map[string]string)
	}

	// Check if secret already exists
	if _, exists := m.secrets[tenantID][secretName]; exists {
		return fmt.Errorf("secret %s already exists for tenant %s", secretName, tenantID)
	}

	m.secrets[tenantID][secretName] = secretValue
	return nil
}

// GetSecret retrieves a secret from the mock Key Vault
func (m *MockClient) GetSecret(ctx context.Context, tenantID, secretName string) (string, error) {
	m.mu.RLock()
	defer m.mu.RUnlock()

	tenantSecrets, ok := m.secrets[tenantID]
	if !ok {
		return "", fmt.Errorf("no secrets found for tenant %s", tenantID)
	}

	secretValue, ok := tenantSecrets[secretName]
	if !ok {
		return "", fmt.Errorf("secret %s not found for tenant %s", secretName, tenantID)
	}

	return secretValue, nil
}

// DeleteSecret deletes a secret from the mock Key Vault
func (m *MockClient) DeleteSecret(ctx context.Context, tenantID, secretName string) error {
	m.mu.Lock()
	defer m.mu.Unlock()

	tenantSecrets, ok := m.secrets[tenantID]
	if !ok {
		return fmt.Errorf("no secrets found for tenant %s", tenantID)
	}

	if _, ok := tenantSecrets[secretName]; !ok {
		return fmt.Errorf("secret %s not found for tenant %s", secretName, tenantID)
	}

	delete(tenantSecrets, secretName)

	// Clean up tenant entry if no more secrets
	if len(tenantSecrets) == 0 {
		delete(m.secrets, tenantID)
	}

	return nil
}

// RevokeSecret revokes a secret (in mock, we just delete it)
func (m *MockClient) RevokeSecret(ctx context.Context, tenantID, secretName string) error {
	return m.DeleteSecret(ctx, tenantID, secretName)
}

// GetAllSecretsForTenant returns all secret names for a tenant (for testing)
func (m *MockClient) GetAllSecretsForTenant(tenantID string) []string {
	m.mu.RLock()
	defer m.mu.RUnlock()

	tenantSecrets, ok := m.secrets[tenantID]
	if !ok {
		return nil
	}

	names := make([]string, 0, len(tenantSecrets))
	for name := range tenantSecrets {
		names = append(names, name)
	}
	return names
}

// HasSecret checks if a secret exists (for testing)
func (m *MockClient) HasSecret(tenantID, secretName string) bool {
	m.mu.RLock()
	defer m.mu.RUnlock()

	tenantSecrets, ok := m.secrets[tenantID]
	if !ok {
		return false
	}

	_, exists := tenantSecrets[secretName]
	return exists
}

// UpdateSecret updates an existing secret with a new value (for rotation)
func (m *MockClient) UpdateSecret(ctx context.Context, tenantID, secretName, newSecretValue string) error {
	m.mu.Lock()
	defer m.mu.Unlock()

	tenantSecrets, ok := m.secrets[tenantID]
	if !ok {
		return fmt.Errorf("no secrets found for tenant %s", tenantID)
	}

	if _, ok := tenantSecrets[secretName]; !ok {
		return fmt.Errorf("secret %s not found for tenant %s", secretName, tenantID)
	}

	tenantSecrets[secretName] = newSecretValue
	return nil
}
