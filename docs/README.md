# MedLogic Platform Documentation

Welcome to the MedLogic Multi-Tenant Medical Platform documentation.

## Documentation Index

### Getting Started

- **[Quick Start Guide](./QUICK_START.md)** - Get up and running in minutes
- **[Deployment Guide](./DEPLOYMENT_GUIDE.md)** - Comprehensive deployment instructions
- **[Troubleshooting Guide](./TROUBLESHOOTING.md)** - Solutions to common issues

### Architecture & Design

- **[Architecture Overview](../README.md)** - System architecture and design decisions
- **[Requirements Document](../.kiro/specs/multi-tenant-medical-platform/requirements.md)** - Functional requirements
- **[Design Document](../.kiro/specs/multi-tenant-medical-platform/design.md)** - Technical design and correctness properties

### Operations

- **[Monitoring & Observability](./OBSERVABILITY_SETUP.md)** - Setting up monitoring stack
- **[Tenant Onboarding](./TENANT_ONBOARDING.md)** - Process for onboarding new tenants
- **[Disaster Recovery](./DISASTER_RECOVERY.md)** - Backup and recovery procedures
- **[Security Best Practices](./SECURITY.md)** - Security guidelines and compliance

### Development

- **[Development Guide](./DEVELOPMENT.md)** - Local development setup
- **[CI/CD Setup](./CICD_SETUP.md)** - Continuous integration and deployment
- **[Testing Strategy](./TESTING.md)** - Unit, integration, and property-based testing
- **[Contributing Guide](./CONTRIBUTING.md)** - How to contribute to the project

## Quick Links

### Deployment Scripts

All scripts are located in the `scripts/` directory:

- `deploy.sh` - Automated deployment script
- `build-and-push.sh` - Build and push container images
- `e2e-test.sh` - End-to-end testing
- `collect-diagnostics.sh` - Collect diagnostic information

### Helm Charts

Helm charts are located in `helm/medlogic-platform/`:

- `Chart.yaml` - Chart metadata
- `values.yaml` - Default configuration values
- `templates/` - Kubernetes resource templates

### Kubernetes Manifests

Raw Kubernetes manifests are in `k8s/`:

- Service deployments
- Network policies
- OPA Gatekeeper policies
- Workload identity configuration

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    External Devices                          │
│                  (medDispense Stations)                      │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                   Smart Gateway (NGINX)                      │
│              Namespace: gateway                              │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                Platform Services                             │
│              Namespace: platform-system                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ Tenant       │  │ Device       │  │ MedLogic     │      │
│  │ Catalog      │  │ Registry     │  │ Service      │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
```

## Key Features

- **Multi-Tenant Architecture**: Strict isolation between hospital tenants
- **Automated Provisioning**: Kubernetes Operator for tenant lifecycle management
- **Smart Routing**: Device-aware gateway with tenant identification
- **Security**: Workload Identity, Key Vault integration, network policies
- **Observability**: Per-tenant metrics, logs, and SLO monitoring
- **Compliance**: HIPAA-ready with encryption at rest and in transit

## Technology Stack

- **Backend**: .NET 9.0, ASP.NET Core, Entity Framework Core
- **Infrastructure**: Kubernetes (AKS), Docker, Helm
- **Gateway**: NGINX with Lua scripting
- **Operator**: Go with kubebuilder
- **Database**: Azure SQL Server with TDE and Always Encrypted
- **Security**: Azure Workload Identity, Azure Key Vault
- **Monitoring**: Prometheus, Loki, Grafana
- **Policy**: OPA Gatekeeper

## Support

### Getting Help

- **Documentation**: Start with the [Quick Start Guide](./QUICK_START.md)
- **Troubleshooting**: Check the [Troubleshooting Guide](./TROUBLESHOOTING.md)
- **Issues**: Create a GitHub issue with diagnostic information
- **Email**: platform@medlogic.io

### Reporting Issues

When reporting issues, please include:

1. Description of the problem
2. Steps to reproduce
3. Expected vs actual behavior
4. Diagnostic bundle (run `scripts/collect-diagnostics.sh`)
5. Environment details (AKS version, region, etc.)

## Contributing

We welcome contributions! Please see the [Contributing Guide](./CONTRIBUTING.md) for details on:

- Code style and standards
- Testing requirements
- Pull request process
- Development workflow

## License

Copyright © 2024 TouchPoint Medical. All rights reserved.

## Changelog

See [CHANGELOG.md](../CHANGELOG.md) for version history and release notes.

## Roadmap

See [ROADMAP.md](../ROADMAP.md) for planned features and improvements.
