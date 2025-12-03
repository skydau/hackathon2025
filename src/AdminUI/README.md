# MedLogic Admin UI

A simple web-based administration interface for the MedLogic multi-tenant medical platform.

## Features

- **Dashboard**: Overview of platform status with key metrics
- **Tenant Management**: Create and view hospital tenants
- **Device Registry**: Register medical devices and associate them with tenants
- **Monitoring**: Embedded Grafana dashboards for observability

## Prerequisites

- .NET 9.0 SDK
- Running instances of:
  - Tenant Catalog Service (default: http://localhost:5000)
  - Device Registry Service (default: http://localhost:5001)
  - Grafana (optional, for monitoring page)

## Configuration

Update `appsettings.json` to configure backend service URLs:

```json
{
  "Services": {
    "TenantCatalog": "http://localhost:5000",
    "DeviceRegistry": "http://localhost:5001"
  }
}
```

## Running Locally

```bash
# From the AdminUI directory
dotnet run

# Or from the solution root
dotnet run --project src/AdminUI
```

The UI will be available at `http://localhost:5002` (or the port shown in the console).

## Usage

### Creating a Tenant

1. Navigate to the "Tenants" page
2. Click "Create New Tenant"
3. Fill in the tenant details:
   - Display Name: Hospital name
   - Database Mode: Choose between per-database or per-schema isolation
   - Database Server: SQL Server connection string
   - Database Name: Name of the tenant database
   - Rate Limit: Requests per second limit
   - SLO settings: Availability and latency targets
4. Click "Create Tenant"

### Registering a Device

1. Navigate to the "Devices" page
2. Click "Register New Device"
3. Fill in the device details:
   - Serial Number: Unique device identifier
   - Tenant: Select the owning tenant
   - Device Type: Type of medical device
4. Click "Register Device"

### Viewing Monitoring

1. Navigate to the "Monitoring" page
2. Enter your Grafana URL (default: http://localhost:3000)
3. Click "Load Dashboard" to embed Grafana visualizations

## Architecture

The Admin UI is built with:
- **Blazor Server**: Interactive server-side rendering
- **Bootstrap 5**: Responsive UI components
- **HTTP Clients**: Communication with backend REST APIs

## Development

The UI follows a simple architecture:
- `Components/Pages/`: Razor pages for each section
- `Services/`: HTTP client wrappers for backend APIs
- `Program.cs`: Service registration and configuration

## Production Deployment

For production deployment:

1. Update `appsettings.json` with production service URLs
2. Build the application:
   ```bash
   dotnet publish -c Release -o ./publish
   ```
3. Deploy to your hosting environment (Azure App Service, Kubernetes, etc.)
4. Ensure proper authentication and authorization are configured

## Security Considerations

This is a demonstration UI with minimal security. For production use:
- Add authentication (Azure AD, OAuth, etc.)
- Implement role-based access control
- Use HTTPS for all connections
- Validate and sanitize all user inputs
- Add CSRF protection
- Implement audit logging
