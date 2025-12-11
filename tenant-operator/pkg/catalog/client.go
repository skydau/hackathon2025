package catalog

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"time"
)

// Client 定义 Tenant Catalog 客户端接口
type Client interface {
	UpdateTenantDbConfig(ctx context.Context, tenantID string, config DatabaseConfig) error
	GetTenantByDisplayName(ctx context.Context, displayName string) (*TenantInfo, error)
}

// TenantCatalogClient 用于与 Tenant Catalog Service 通信
type TenantCatalogClient struct {
	baseURL    string
	httpClient *http.Client
	maxRetries int
}

// DatabaseConfig 表示数据库配置
type DatabaseConfig struct {
	Server      string `json:"dbServer"`
	Database    string `json:"dbDatabase"`
	Username    string `json:"dbUsername"`
	PasswordRef string `json:"dbPasswordRef"`
}

// TenantInfo 表示租户信息
type TenantInfo struct {
	ID          string `json:"id"`
	DisplayName string `json:"displayName"`
	Status      string `json:"status"`
}

// NewTenantCatalogClient 创建新的 TenantCatalogClient 实例
// baseURL: Tenant Catalog Service 的基础 URL，例如 "http://tenant-catalog.platform-system.svc.cluster.local:8080"
// maxRetries: 最大重试次数，建议设置为 3
func NewTenantCatalogClient(baseURL string, maxRetries int) *TenantCatalogClient {
	if maxRetries <= 0 {
		maxRetries = 3 // 默认重试 3 次
	}

	return &TenantCatalogClient{
		baseURL:    baseURL,
		maxRetries: maxRetries,
		httpClient: &http.Client{
			Timeout: 30 * time.Second,
			Transport: &http.Transport{
				MaxIdleConns:        100,
				MaxIdleConnsPerHost: 10,
				IdleConnTimeout:     90 * time.Second,
			},
		},
	}
}

// UpdateTenantDbConfig 更新租户的数据库配置
// 该方法会调用 Tenant Catalog 的 PATCH /api/tenants/{id} 端点
// 支持自动重试机制，在网络错误或 5xx 错误时会重试
func (c *TenantCatalogClient) UpdateTenantDbConfig(
	ctx context.Context,
	tenantID string,
	config DatabaseConfig,
) error {
	url := fmt.Sprintf("%s/api/tenants/%s", c.baseURL, tenantID)

	jsonData, err := json.Marshal(config)
	if err != nil {
		return fmt.Errorf("failed to marshal database config: %w", err)
	}

	var lastErr error
	for attempt := 0; attempt <= c.maxRetries; attempt++ {
		if attempt > 0 {
			// 指数退避：第一次重试等待 1 秒，第二次 2 秒，第三次 4 秒
			backoff := time.Duration(1<<uint(attempt-1)) * time.Second
			select {
			case <-ctx.Done():
				return fmt.Errorf("context cancelled during retry backoff: %w", ctx.Err())
			case <-time.After(backoff):
			}
		}

		req, err := http.NewRequestWithContext(ctx, http.MethodPatch, url, bytes.NewBuffer(jsonData))
		if err != nil {
			return fmt.Errorf("failed to create HTTP request: %w", err)
		}

		req.Header.Set("Content-Type", "application/json")
		req.Header.Set("Accept", "application/json")

		resp, err := c.httpClient.Do(req)
		if err != nil {
			lastErr = fmt.Errorf("HTTP request failed (attempt %d/%d): %w", attempt+1, c.maxRetries+1, err)
			continue // 网络错误，重试
		}

		// 读取响应体
		body, readErr := io.ReadAll(resp.Body)
		resp.Body.Close()

		// 检查状态码
		if resp.StatusCode >= 200 && resp.StatusCode < 300 {
			// 成功
			return nil
		}

		// 4xx 错误不重试（客户端错误）
		if resp.StatusCode >= 400 && resp.StatusCode < 500 {
			if readErr != nil {
				return fmt.Errorf("request failed with status %d (unable to read response body: %v)", resp.StatusCode, readErr)
			}
			return fmt.Errorf("request failed with status %d: %s", resp.StatusCode, string(body))
		}

		// 5xx 错误重试（服务器错误）
		if readErr != nil {
			lastErr = fmt.Errorf("server error with status %d (attempt %d/%d, unable to read response body: %v)",
				resp.StatusCode, attempt+1, c.maxRetries+1, readErr)
		} else {
			lastErr = fmt.Errorf("server error with status %d (attempt %d/%d): %s",
				resp.StatusCode, attempt+1, c.maxRetries+1, string(body))
		}
	}

	return fmt.Errorf("failed to update tenant database config after %d attempts: %w", c.maxRetries+1, lastErr)
}

// GetTenantByDisplayName 通过displayName获取租户信息
func (c *TenantCatalogClient) GetTenantByDisplayName(
	ctx context.Context,
	displayName string,
) (*TenantInfo, error) {
	url := fmt.Sprintf("%s/api/tenants/by-name/%s", c.baseURL, displayName)

	var lastErr error
	for attempt := 0; attempt <= c.maxRetries; attempt++ {
		if attempt > 0 {
			backoff := time.Duration(1<<uint(attempt-1)) * time.Second
			select {
			case <-ctx.Done():
				return nil, fmt.Errorf("context cancelled during retry backoff: %w", ctx.Err())
			case <-time.After(backoff):
			}
		}

		req, err := http.NewRequestWithContext(ctx, http.MethodGet, url, nil)
		if err != nil {
			return nil, fmt.Errorf("failed to create HTTP request: %w", err)
		}

		req.Header.Set("Accept", "application/json")

		resp, err := c.httpClient.Do(req)
		if err != nil {
			lastErr = fmt.Errorf("HTTP request failed (attempt %d/%d): %w", attempt+1, c.maxRetries+1, err)
			continue
		}

		body, readErr := io.ReadAll(resp.Body)
		resp.Body.Close()

		if resp.StatusCode >= 200 && resp.StatusCode < 300 {
			var tenantInfo TenantInfo
			if err := json.Unmarshal(body, &tenantInfo); err != nil {
				return nil, fmt.Errorf("failed to unmarshal response: %w", err)
			}
			return &tenantInfo, nil
		}

		if resp.StatusCode >= 400 && resp.StatusCode < 500 {
			if readErr != nil {
				return nil, fmt.Errorf("request failed with status %d (unable to read response body: %v)", resp.StatusCode, readErr)
			}
			return nil, fmt.Errorf("request failed with status %d: %s", resp.StatusCode, string(body))
		}

		if readErr != nil {
			lastErr = fmt.Errorf("server error with status %d (attempt %d/%d, unable to read response body: %v)",
				resp.StatusCode, attempt+1, c.maxRetries+1, readErr)
		} else {
			lastErr = fmt.Errorf("server error with status %d (attempt %d/%d): %s",
				resp.StatusCode, attempt+1, c.maxRetries+1, string(body))
		}
	}

	return nil, fmt.Errorf("failed to get tenant by display name after %d attempts: %w", c.maxRetries+1, lastErr)
}
