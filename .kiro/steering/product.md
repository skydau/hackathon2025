# Product Overview

MedLogic Platform is a cloud-native multi-tenant medical backend system designed for TouchPoint Medical. The platform enables multiple hospitals (tenants) to securely share infrastructure while maintaining strict data isolation and security boundaries.

## Core Capabilities

- **Tenant Lifecycle Management**: Automated onboarding and provisioning of hospital tenants using Kubernetes Operator pattern
- **Device-to-Tenant Routing**: Smart gateway that routes medical device requests (medDispense stations) to the correct tenant backend
- **Data Isolation**: Per-tenant database isolation with encryption at rest and in transit
- **Security & Compliance**: Multi-layer security with workload identity, network policies, and healthcare-grade data protection

## Key Services

- **Tenant Catalog Service**: Central registry for tenant metadata and configuration
- **Device Registry Service**: Maps physical devices to their owning tenants
- **Smart Gateway (NGINX)**: Entry point for device traffic with tenant-aware routing and rate limiting
- **Tenant Operator**: Kubernetes controller that automates tenant resource provisioning
