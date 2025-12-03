package v1

import (
	metav1 "k8s.io/apimachinery/pkg/apis/meta/v1"
)

// TenantSpec defines the desired state of Tenant
type TenantSpec struct {
	// DisplayName is the human-readable name of the tenant
	DisplayName string `json:"displayName"`

	// DB contains database configuration for the tenant
	DB DatabaseConfig `json:"db"`

	// Throttling contains rate limiting configuration
	Throttling ThrottlingConfig `json:"throttling,omitempty"`

	// SLO contains service level objective configuration
	SLO SLOConfig `json:"slo,omitempty"`
}

// DatabaseConfig defines database configuration for a tenant
type DatabaseConfig struct {
	// Mode is either "perDatabase" or "perSchema"
	Mode string `json:"mode"`

	// Server is the database server address
	Server string `json:"server"`

	// Database is the database name
	Database string `json:"database"`

	// Schema is the schema name (for perSchema mode)
	Schema string `json:"schema,omitempty"`
}

// ThrottlingConfig defines rate limiting configuration
type ThrottlingConfig struct {
	// RPS is requests per second limit
	RPS int `json:"rps,omitempty"`
}

// SLOConfig defines service level objectives
type SLOConfig struct {
	// Availability target (e.g., "99.9%")
	Availability string `json:"availability,omitempty"`

	// P95LatencyMs is the p95 latency target in milliseconds
	P95LatencyMs int `json:"p95_latency_ms,omitempty"`
}

// TenantStatus defines the observed state of Tenant
type TenantStatus struct {
	// Phase represents the current phase of tenant provisioning
	Phase string `json:"phase,omitempty"`

	// Conditions represent the latest available observations of the tenant's state
	Conditions []metav1.Condition `json:"conditions,omitempty"`

	// NamespaceCreated indicates if the namespace has been created
	NamespaceCreated bool `json:"namespaceCreated,omitempty"`

	// ResourcesProvisioned indicates if all resources have been provisioned
	ResourcesProvisioned bool `json:"resourcesProvisioned,omitempty"`
}

// +kubebuilder:object:root=true
// +kubebuilder:subresource:status
// +kubebuilder:resource:scope=Cluster

// Tenant is the Schema for the tenants API
type Tenant struct {
	metav1.TypeMeta   `json:",inline"`
	metav1.ObjectMeta `json:"metadata,omitempty"`

	Spec   TenantSpec   `json:"spec,omitempty"`
	Status TenantStatus `json:"status,omitempty"`
}

// +kubebuilder:object:root=true

// TenantList contains a list of Tenant
type TenantList struct {
	metav1.TypeMeta `json:",inline"`
	metav1.ListMeta `json:"metadata,omitempty"`
	Items           []Tenant `json:"items"`
}

func init() {
	SchemeBuilder.Register(&Tenant{}, &TenantList{})
}
