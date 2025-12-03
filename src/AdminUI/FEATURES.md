# Admin UI Features

## Dashboard Overview

The home page provides a comprehensive overview of the MedLogic platform:

### Metrics Cards
- **Total Tenants**: Count of all registered hospital tenants
- **Registered Devices**: Total number of medical devices in the system
- **Active Tenants**: Number of tenants with "Enabled" status
- **System Status**: Overall platform health indicator

### Quick Actions
Direct navigation buttons to:
- Manage Tenants
- Register Devices
- View Monitoring

### Recent Activity
- List of recently created tenants
- Status badges for each tenant
- Creation timestamps

### Platform Information
Overview of MedLogic platform capabilities and key features

## Tenant Management

### View All Tenants
- **Table View**: Displays all tenants with key information
  - Tenant ID (unique identifier)
  - Display Name (hospital name)
  - Status (with color-coded badges)
  - Database Mode (perDatabase or perSchema)
  - Rate Limit (requests per second)
  - Creation timestamp

### Create New Tenant
Interactive form with the following fields:

#### Basic Information
- **Display Name**: Hospital or organization name
- **Database Mode**: Choose isolation strategy
  - Database per Tenant (dedicated database)
  - Schema per Tenant (shared database, separate schema)

#### Database Configuration
- **Database Server**: SQL Server connection endpoint
- **Database Name**: Name of the tenant's database
- **Schema Name**: (Only for Schema-per-Tenant mode)

#### Performance Settings
- **Rate Limit (RPS)**: Maximum requests per second
  - Default: 100 RPS
  - Prevents single tenant from overwhelming the system

#### SLO Configuration
- **Availability Target**: e.g., "99.9%"
  - Defines uptime commitment
- **P95 Latency Target**: Maximum acceptable latency in milliseconds
  - Default: 1000ms
  - Used for monitoring and alerting

### Status Indicators
Color-coded badges for tenant status:
- 🟢 **Enabled**: Tenant is active and operational
- 🟡 **Provisioning**: Tenant is being set up
- ⚫ **Disabled**: Tenant is temporarily inactive
- 🔴 **Decommissioned**: Tenant has been removed

## Device Registry

### View All Devices
- **Table View**: Displays all registered devices
  - Serial Number (unique device identifier)
  - Tenant ID (owning tenant)
  - Device Type (with badge)
  - Registration timestamp
  - Last Seen timestamp

### Register New Device
Interactive form with the following fields:

#### Device Information
- **Serial Number**: Unique identifier for the device
  - Example: MED-12345
  - Must be unique across the platform

#### Tenant Association
- **Tenant Dropdown**: Select owning hospital
  - Populated from Tenant Catalog
  - Shows tenant display name and ID

#### Device Type
- **medDispense Station**: Medication dispensing device
- **Medical Monitor**: Patient monitoring equipment
- **Infusion Pump**: IV medication delivery system

### Device Status
- **Registered**: Timestamp when device was added
- **Last Seen**: Last communication from device
  - Shows "Never" if device hasn't connected yet

## Monitoring Dashboard

### Grafana Integration
- **Configurable URL**: Enter your Grafana instance URL
- **Embedded Dashboards**: View Grafana visualizations directly in the UI
- **Full-Screen Option**: Link to open Grafana in new tab

### Quick Metrics
Summary cards showing:
- Total number of tenants
- Active device count
- System health status

### Dashboard Links
Direct links to specific Grafana dashboards:

#### Tenant Overview
- Per-tenant metrics
- SLO tracking and compliance
- Resource usage by tenant

#### Platform Health
- Overall system performance
- Service availability
- Error rates and latency

#### Log Explorer
- Query logs with Loki
- Filter by tenant ID
- Search across all services

### Monitoring Features
- **Real-time Updates**: Dashboards refresh automatically
- **Tenant Filtering**: View metrics for specific tenants
- **Time Range Selection**: Analyze historical data
- **Alert Status**: View active alerts and SLO violations

## User Experience Features

### Loading States
- Spinner indicators during data fetching
- Prevents user confusion during async operations

### Error Handling
- Clear error messages when operations fail
- Suggestions for resolving common issues
- Graceful degradation when services unavailable

### Success Feedback
- Confirmation messages after successful operations
- Auto-dismiss after 2 seconds
- Automatic list refresh

### Responsive Design
- Mobile-friendly layout
- Bootstrap grid system
- Works on tablets and phones

### Form Validation
- Required field indicators
- Client-side validation
- Helpful error messages

### Empty States
- Informative messages when no data exists
- Call-to-action buttons to get started
- Helpful guidance for new users

## Navigation

### Main Menu
- **Dashboard**: Platform overview and quick actions
- **Tenants**: Tenant management interface
- **Devices**: Device registry interface
- **Monitoring**: Observability and metrics

### Breadcrumbs
- Clear indication of current location
- Easy navigation back to previous pages

## Configuration

### Service Endpoints
Configurable backend service URLs:
- Tenant Catalog Service
- Device Registry Service

### Environment Support
- Development: Local services
- Production: Kubernetes services
- Custom: User-defined endpoints

## Accessibility

### Keyboard Navigation
- Tab through form fields
- Enter to submit forms
- Escape to cancel operations

### Screen Reader Support
- Semantic HTML elements
- ARIA labels where needed
- Descriptive button text

### Visual Indicators
- Color-coded status badges
- Icons for actions
- Clear typography hierarchy

## Performance

### Optimizations
- Minimal HTTP requests
- Efficient data loading
- Client-side caching (browser)

### Async Operations
- Non-blocking UI updates
- Background data fetching
- Smooth user experience

## Security Features

### Input Validation
- Client-side validation
- Required field enforcement
- Format validation

### Error Messages
- No sensitive information exposed
- Generic error messages
- Detailed logging (server-side)

## Future Roadmap

Planned enhancements:
1. Authentication and authorization
2. Tenant status updates (Enable/Disable)
3. Device management (Update/Delete)
4. Search and filtering
5. Pagination for large datasets
6. Export to CSV
7. Audit logging
8. Real-time notifications
9. Custom dashboard creation
10. Multi-language support

## Support

For issues or questions:
- Check the [Quick Start Guide](QUICK_START.md)
- Review the [Implementation Summary](IMPLEMENTATION_SUMMARY.md)
- Consult the main [README](README.md)
