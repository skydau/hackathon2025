# MedLogic Platform Deployment Checklist

Use this checklist to ensure all steps are completed for a successful deployment.

## Pre-Deployment

### Azure Resources

- [ ] Azure subscription created and accessible
- [ ] Resource group created
- [ ] AKS cluster created with:
  - [ ] Workload Identity enabled
  - [ ] OIDC issuer enabled
  - [ ] Azure CNI networking
  - [ ] Minimum 3 nodes
- [ ] Azure Container Registry (ACR) created
- [ ] ACR attached to AKS cluster
- [ ] Azure SQL Server created
- [ ] TenantCatalog database created
- [ ] DeviceRegistry database created
- [ ] SQL Server firewall rules configured
- [ ] Azure Key Vault created
- [ ] Key Vault RBAC enabled

### Local Environment

- [ ] kubectl installed (v1.28+)
- [ ] Helm installed (v3.12+)
- [ ] Azure CLI installed (v2.50+)
- [ ] Docker installed (v24.0+)
- [ ] .NET SDK installed (v9.0+)
- [ ] Go installed (v1.21+)
- [ ] kubectl configured to access AKS cluster
- [ ] Azure CLI logged in

### Workload Identity Setup

- [ ] Azure AD app registrations created for:
  - [ ] Tenant Catalog Service
  - [ ] Device Registry Service
  - [ ] MedLogic Service
  - [ ] Tenant Operator
- [ ] Federated credentials configured for each app
- [ ] Service accounts created in Kubernetes
- [ ] Service accounts annotated with client IDs
- [ ] RBAC permissions granted:
  - [ ] Key Vault Secrets User
  - [ ] SQL Server Contributor (if using managed identity for SQL)

### Configuration Files

- [ ] values-production.yaml created and customized
- [ ] All placeholder values replaced:
  - [ ] Image registry URLs
  - [ ] Azure client IDs
  - [ ] Azure tenant IDs
  - [ ] Database server names
  - [ ] Key Vault names
  - [ ] Subscription IDs
  - [ ] Resource group names

## Build and Push

- [ ] .NET solution builds successfully
- [ ] All unit tests pass
- [ ] All property-based tests pass
- [ ] Container images built:
  - [ ] tenant-catalog-service
  - [ ] device-registry-service
  - [ ] medlogic-service
  - [ ] admin-ui
  - [ ] smart-gateway
  - [ ] tenant-operator
- [ ] All images pushed to ACR
- [ ] Image tags documented

## Deployment

### Helm Deployment

- [ ] Helm chart validated (helm lint)
- [ ] Dry run executed successfully
- [ ] Helm chart installed/upgraded
- [ ] Deployment completed without errors

### Verify Resources

- [ ] Namespaces created:
  - [ ] platform-system
  - [ ] gateway
  - [ ] observability (if enabled)
- [ ] All deployments created
- [ ] All pods running and ready
- [ ] All services created
- [ ] LoadBalancer service has external IP

### Database Setup

- [ ] Database migrations applied to TenantCatalog
- [ ] Database migrations applied to DeviceRegistry
- [ ] TDE enabled on databases
- [ ] Always Encrypted configured (if applicable)
- [ ] Database backups configured

### Security

- [ ] Network policies applied
- [ ] OPA Gatekeeper installed
- [ ] Gatekeeper constraints applied:
  - [ ] Tenant label constraint
  - [ ] No cross-tenant NetworkPolicy constraint
- [ ] Secrets stored in Key Vault
- [ ] No secrets in ConfigMaps or environment variables
- [ ] RBAC roles configured

## Post-Deployment

### Health Checks

- [ ] All pods healthy (kubectl get pods)
- [ ] Health endpoints responding:
  - [ ] Tenant Catalog: /health
  - [ ] Device Registry: /health
  - [ ] MedLogic Service: /health
  - [ ] Smart Gateway: /health
- [ ] No CrashLoopBackOff pods
- [ ] No ImagePullBackOff errors

### Functional Testing

- [ ] Create test tenant via API
- [ ] Retrieve tenant via API
- [ ] Update tenant status
- [ ] Register test device
- [ ] Query device-to-tenant mapping
- [ ] End-to-end test script passes
- [ ] Rate limiting verified
- [ ] Tenant isolation verified

### Tenant Operator

- [ ] Tenant CRD installed
- [ ] Operator pod running
- [ ] Create test Tenant CR
- [ ] Verify namespace created
- [ ] Verify ConfigMap created
- [ ] Verify ResourceQuota applied
- [ ] Verify NetworkPolicy applied
- [ ] Verify RBAC configured
- [ ] Verify Key Vault secrets created

### Monitoring & Observability

- [ ] Prometheus deployed (if enabled)
- [ ] Loki deployed (if enabled)
- [ ] Grafana deployed (if enabled)
- [ ] Metrics being collected
- [ ] Logs being aggregated
- [ ] Dashboards accessible
- [ ] SLO rules configured
- [ ] Alert rules configured
- [ ] Test alerts firing correctly

### Documentation

- [ ] Deployment documented
- [ ] Configuration values documented
- [ ] Access credentials documented (securely)
- [ ] Runbook created for common operations
- [ ] Disaster recovery plan documented
- [ ] Contact information updated

## Production Readiness

### Performance

- [ ] Load testing completed
- [ ] Performance benchmarks met
- [ ] Resource limits tuned
- [ ] Autoscaling configured
- [ ] Connection pooling optimized

### High Availability

- [ ] Multiple replicas for all services
- [ ] Pod anti-affinity configured
- [ ] PodDisruptionBudgets configured
- [ ] Health checks tuned
- [ ] Liveness and readiness probes verified

### Backup & Recovery

- [ ] Database backup schedule configured
- [ ] Backup retention policy set
- [ ] Restore procedure tested
- [ ] Disaster recovery plan tested
- [ ] RTO and RPO documented

### Security Audit

- [ ] Security scan completed on all images
- [ ] No critical vulnerabilities
- [ ] Network policies tested
- [ ] Gatekeeper policies tested
- [ ] Secrets rotation procedure documented
- [ ] Audit logging enabled
- [ ] Compliance requirements verified (HIPAA, etc.)

### Operational Readiness

- [ ] Monitoring alerts configured
- [ ] On-call rotation established
- [ ] Escalation procedures documented
- [ ] Troubleshooting guide reviewed
- [ ] Team trained on operations
- [ ] Support contacts documented

## Sign-Off

### Technical Sign-Off

- [ ] Platform Engineer: _________________ Date: _______
- [ ] DevOps Lead: _________________ Date: _______
- [ ] Security Engineer: _________________ Date: _______
- [ ] Database Administrator: _________________ Date: _______

### Business Sign-Off

- [ ] Product Owner: _________________ Date: _______
- [ ] Project Manager: _________________ Date: _______

## Post-Deployment Tasks

### Week 1

- [ ] Monitor system stability
- [ ] Review logs for errors
- [ ] Verify all alerts working
- [ ] Conduct user acceptance testing
- [ ] Document any issues

### Week 2-4

- [ ] Review performance metrics
- [ ] Optimize resource allocation
- [ ] Fine-tune autoscaling
- [ ] Conduct security review
- [ ] Update documentation

### Monthly

- [ ] Review SLO compliance
- [ ] Analyze cost metrics
- [ ] Plan capacity upgrades
- [ ] Review and update runbooks
- [ ] Conduct disaster recovery drill

## Rollback Plan

If deployment fails:

- [ ] Rollback procedure documented
- [ ] Previous version images available
- [ ] Database rollback scripts prepared
- [ ] Rollback tested in staging
- [ ] Rollback decision criteria defined

## Notes

Use this section to document any deployment-specific notes, issues encountered, or deviations from the standard process:

```
Date: _______________
Deployed by: _______________
Environment: _______________

Notes:
_________________________________________________________________
_________________________________________________________________
_________________________________________________________________
```
