# Saint Henri Basketball API

This repository contains the backend API for the Saint Henri Basketball application. The API handles user registration, session management, attendance tracking, payment processing, and email notifications.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Configuration](#configuration)
- [Database](#database)
- [Email Service](#email-service)
- [Authentication](#authentication)
- [API Documentation](#api-documentation)
- [Deployment](#deployment)

## Prerequisites

- .NET SDK 7.0 or higher
- SQL Server (local or remote)
- Azure Account (for production deployment)

## Getting Started

1. Clone the repository:
   ```bash
   git clone https://github.com/fsawadogo/SaintHenriBasketball.git
   cd SaintHenriBasketball
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Configure your development environment (see [Development Setup](#development-setup))

4. Run the application:
   ```bash
   dotnet run
   ```

## Development Setup

### User Secrets (Recommended for development)

We use the .NET User Secrets feature to manage sensitive information during development:

1. Initialize user secrets for the project:
   ```bash
   dotnet user-secrets init
   ```

2. Add required secrets:
   ```bash
   dotnet user-secrets set "Resend:ApiKey" "your-resend-api-key"
   dotnet user-secrets set "JwtSettings:Key" "your-jwt-signing-key"
   ```

### Environment Variables

Alternatively, you can use environment variables:

```bash
# Windows
set Resend__ApiKey=your-resend-api-key
set JwtSettings__Key=your-jwt-signing-key

# macOS/Linux
export Resend__ApiKey=your-resend-api-key
export JwtSettings__Key=your-jwt-signing-key
```

## Configuration

The application uses a tiered configuration approach:

1. **Base configuration**: `appsettings.json`
2. **Environment-specific**: `appsettings.Development.json`, `appsettings.Production.json`
3. **Secrets**: User Secrets (dev) or Azure Key Vault (prod)

### Configuration Files

- `appsettings.json` - Common settings for all environments
- `appsettings.Development.json` - Development-specific settings
- `appsettings.Production.json` - Production-specific settings (if needed)

## Database

The application uses Entity Framework Core with SQL Server. Migrations are automatically applied on startup.

### Creating Migrations

```bash
dotnet ef migrations add MigrationName
dotnet ef database update
```

## Email Service

Email functionality is provided through Resend. The service handles:

- User registration and password reset emails
- Payment confirmations
- Session attendance notifications
- Season registration updates

### Testing Email Functionality

For development, we recommend using:
1. A Resend test API key
2. Test recipient addresses (e.g., your own email)

## Authentication

The API uses JWT (JSON Web Token) authentication. Tokens are issued upon login and must be included in the Authorization header for protected endpoints.

## API Documentation

API documentation is available via Swagger UI at `/swagger` when the application is running.

## Deployment

### Azure Deployment Checklist

1. Create necessary Azure resources:
    - App Service
    - SQL Database
    - Key Vault

2. Configure Key Vault:
    - Add all required secrets
    - Grant access to the App Service's managed identity

3. Configure App Service:
    - Enable managed identity
    - Set KeyVaultName app setting
    - Configure database connection string

4. Deploy using your preferred method:
    - Azure DevOps Pipelines
    - GitHub Actions
    - Visual Studio Publish

### Important Production Settings

Ensure these settings are properly configured in production:

- Database connection string (stored as an App Service connection string)
- KeyVaultName (points to your Azure Key Vault)
- Application Insights for monitoring (recommended)