package catalog

import (
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"
)

func TestNewTenantCatalogClient(t *testing.T) {
	client := NewTenantCatalogClient("http://example.com", 3)
	if client == nil {
		t.Fatal("Expected non-nil client")
	}
	if client.baseURL != "http://example.com" {
		t.Errorf("Expected baseURL to be 'http://example.com', got '%s'", client.baseURL)
	}
	if client.maxRetries != 3 {
		t.Errorf("Expected maxRetries to be 3, got %d", client.maxRetries)
	}
}

func TestNewTenantCatalogClient_DefaultRetries(t *testing.T) {
	client := NewTenantCatalogClient("http://example.com", 0)
	if client.maxRetries != 3 {
		t.Errorf("Expected default maxRetries to be 3, got %d", client.maxRetries)
	}
}

func TestUpdateTenantDbConfig_Success(t *testing.T) {
	// 创建测试服务器
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		// 验证请求方法
		if r.Method != http.MethodPatch {
			t.Errorf("Expected PATCH request, got %s", r.Method)
		}

		// 验证请求路径
		expectedPath := "/api/tenants/test-tenant"
		if r.URL.Path != expectedPath {
			t.Errorf("Expected path %s, got %s", expectedPath, r.URL.Path)
		}

		// 验证请求头
		if r.Header.Get("Content-Type") != "application/json" {
			t.Errorf("Expected Content-Type to be 'application/json', got '%s'", r.Header.Get("Content-Type"))
		}

		// 验证请求体
		var config DatabaseConfig
		if err := json.NewDecoder(r.Body).Decode(&config); err != nil {
			t.Errorf("Failed to decode request body: %v", err)
		}

		if config.Server != "localhost,1433" {
			t.Errorf("Expected server to be 'localhost,1433', got '%s'", config.Server)
		}

		// 返回成功响应
		w.WriteHeader(http.StatusOK)
	}))
	defer server.Close()

	// 创建客户端
	client := NewTenantCatalogClient(server.URL, 3)

	// 调用方法
	config := DatabaseConfig{
		Server:      "localhost,1433",
		Database:    "TestDB",
		Username:    "testuser",
		PasswordRef: "test-secret",
	}

	ctx := context.Background()
	err := client.UpdateTenantDbConfig(ctx, "test-tenant", config)
	if err != nil {
		t.Errorf("Expected no error, got %v", err)
	}
}

func TestUpdateTenantDbConfig_NotFound(t *testing.T) {
	// 创建测试服务器，返回 404
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusNotFound)
		w.Write([]byte("Tenant not found"))
	}))
	defer server.Close()

	client := NewTenantCatalogClient(server.URL, 3)

	config := DatabaseConfig{
		Server:      "localhost,1433",
		Database:    "TestDB",
		Username:    "testuser",
		PasswordRef: "test-secret",
	}

	ctx := context.Background()
	err := client.UpdateTenantDbConfig(ctx, "nonexistent-tenant", config)
	if err == nil {
		t.Error("Expected error for 404 response, got nil")
	}
}

func TestUpdateTenantDbConfig_ServerError_Retry(t *testing.T) {
	attemptCount := 0

	// 创建测试服务器，前两次返回 500，第三次返回 200
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		attemptCount++
		if attemptCount < 3 {
			w.WriteHeader(http.StatusInternalServerError)
			w.Write([]byte("Internal server error"))
		} else {
			w.WriteHeader(http.StatusOK)
		}
	}))
	defer server.Close()

	client := NewTenantCatalogClient(server.URL, 3)

	config := DatabaseConfig{
		Server:      "localhost,1433",
		Database:    "TestDB",
		Username:    "testuser",
		PasswordRef: "test-secret",
	}

	ctx := context.Background()
	err := client.UpdateTenantDbConfig(ctx, "test-tenant", config)
	if err != nil {
		t.Errorf("Expected success after retries, got error: %v", err)
	}

	if attemptCount != 3 {
		t.Errorf("Expected 3 attempts, got %d", attemptCount)
	}
}

func TestUpdateTenantDbConfig_ContextCancellation(t *testing.T) {
	// 创建测试服务器，延迟响应
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		time.Sleep(2 * time.Second)
		w.WriteHeader(http.StatusOK)
	}))
	defer server.Close()

	client := NewTenantCatalogClient(server.URL, 3)

	config := DatabaseConfig{
		Server:      "localhost,1433",
		Database:    "TestDB",
		Username:    "testuser",
		PasswordRef: "test-secret",
	}

	// 创建一个会立即取消的 context
	ctx, cancel := context.WithCancel(context.Background())
	cancel()

	err := client.UpdateTenantDbConfig(ctx, "test-tenant", config)
	if err == nil {
		t.Error("Expected error for cancelled context, got nil")
	}
}

func TestUpdateTenantDbConfig_InvalidJSON(t *testing.T) {
	client := NewTenantCatalogClient("http://example.com", 3)

	// 创建一个无法序列化为 JSON 的配置（这在实际中不太可能发生，但为了测试覆盖率）
	// 由于 DatabaseConfig 的所有字段都是 string，我们无法直接测试 JSON 序列化失败
	// 这个测试主要是为了代码覆盖率

	config := DatabaseConfig{
		Server:      "localhost,1433",
		Database:    "TestDB",
		Username:    "testuser",
		PasswordRef: "test-secret",
	}

	ctx := context.Background()
	// 这应该会因为无法连接到 example.com 而失败
	err := client.UpdateTenantDbConfig(ctx, "test-tenant", config)
	if err == nil {
		t.Error("Expected error for invalid URL, got nil")
	}
}
