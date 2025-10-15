# Azure Verified Modules (AVM) Reference

This document provides details about the Azure Verified Modules used in `main-avm.bicep`.

## What are Azure Verified Modules?

[Azure Verified Modules (AVM)](https://aka.ms/avm) are the official, Microsoft-supported Bicep modules for deploying Azure resources. They provide:

- **Official Support**: Maintained by Microsoft with regular updates
- **Best Practices**: Security, governance, and operational excellence built-in
- **Standardization**: Consistent parameter names and patterns
- **Comprehensive Testing**: Validated across multiple scenarios
- **Semantic Versioning**: Predictable updates and compatibility

## Modules Used in This Project

### Network Resources

#### Virtual Network
- **Module**: `avm/res/network/virtual-network:0.5.2`
- **Purpose**: Creates VNet with subnets for Function App integration and private endpoints
- **Documentation**: [Virtual Network Module](https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/network/virtual-network)
- **Key Features**:
  - Subnet delegation for Azure Functions
  - Private endpoint support
  - Network security configuration

#### Private DNS Zones
- **Module**: `avm/res/network/private-dns-zone:0.6.0`
- **Purpose**: Creates private DNS zones for Azure services
- **Documentation**: [Private DNS Zone Module](https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/network/private-dns-zone)
- **Zones Created**:
  - `privatelink.blob.core.windows.net`
  - `privatelink.queue.core.windows.net`
  - `privatelink.table.core.windows.net`
  - `privatelink.documents.azure.com`
  - `privatelink.cognitiveservices.azure.com`
  - `privatelink.azurewebsites.net`

### Monitoring Resources

#### Log Analytics Workspace
- **Module**: `avm/res/operational-insights/workspace:0.9.1`
- **Purpose**: Centralized logging and analytics
- **Documentation**: [Log Analytics Module](https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/operational-insights/workspace)
- **Configuration**:
  - SKU: PerGB2018 (pay-as-you-go)
  - Retention: 30 days
  - Public access for ingestion and query

#### Application Insights
- **Module**: `avm/res/insights/component:0.4.2`
- **Purpose**: Application performance monitoring
- **Documentation**: [Application Insights Module](https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/insights/component)
- **Configuration**:
  - Type: Web application
  - Workspace-based (linked to Log Analytics)

### Storage Resources

#### Storage Account
- **Module**: `avm/res/storage/storage-account:0.14.3`
- **Purpose**: Blob storage, queues, and tables with private endpoints
- **Documentation**: [Storage Account Module](https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/storage/storage-account)
- **Key Features**:
  - Private endpoints for blob, queue, and table services
  - No public network access
  - Built-in containers and queues
  - Network ACL configuration

**Containers Created**:
- `uploaded-pdfs` - Input PDF files
- `processed-documents` - Processed documents and results

**Queue Created**:
- `pdf-processing-queue` - Processing queue

### Cognitive Services

#### Document Intelligence (Form Recognizer)
- **Module**: `avm/res/cognitive-services/account:0.9.0`
- **Purpose**: OCR and document analysis
- **Documentation**: [Cognitive Services Module](https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/cognitive-services/account)
- **Configuration**:
  - Kind: FormRecognizer
  - SKU: S0 (Standard)
  - Private endpoint for secure access
  - Custom subdomain for routing

### Database Resources

#### Cosmos DB
- **Module**: `avm/res/document-db/database-account:0.11.1`
- **Purpose**: Document storage with serverless configuration
- **Documentation**: [Cosmos DB Module](https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/document-db/database-account)
- **Configuration**:
  - Serverless capacity mode
  - SQL API
  - Private endpoint
  - Single region deployment

**Database**: `DocumentOcrDb`
**Container**: `ProcessedDocuments` (partitioned by `/identifier`)

### Compute Resources

#### App Service Plan
- **Module**: `avm/res/web/serverfarm:0.4.0`
- **Purpose**: Hosting plan for Function App
- **Documentation**: [App Service Plan Module](https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/web/serverfarm)
- **Configuration**:
  - OS: Linux
  - SKU: Premium P1v3 (required for VNet integration)
  - Capacity: 1 instance

#### Function App
- **Module**: `avm/res/web/site:0.11.1`
- **Purpose**: Azure Functions hosting
- **Documentation**: [Web Site Module](https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/web/site)
- **Key Features**:
  - .NET 8 Isolated runtime
  - VNet integration for outbound traffic
  - Private endpoint for inbound traffic
  - System-assigned managed identity
  - Always On enabled

## Module Versioning

All modules use semantic versioning (e.g., `0.14.3`):
- **Major version** (0): Breaking changes
- **Minor version** (14): New features, backward compatible
- **Patch version** (3): Bug fixes

## Updating Modules

To update to the latest module versions:

1. Check the [AVM Module Index](https://azure.github.io/Azure-Verified-Modules/)
2. Update version numbers in `main-avm.bicep`
3. Review release notes for breaking changes
4. Test in a dev environment first

Example update:
```bicep
// Before
module storage 'br/public:avm/res/storage/storage-account:0.14.3' = {

// After (hypothetical newer version)
module storage 'br/public:avm/res/storage/storage-account:0.15.0' = {
```

## Custom Modules

The following resources still use custom modules as AVM doesn't provide comprehensive coverage:

### Role Assignments
- **File**: `modules/roleAssignments.bicep`
- **Reason**: Complex role assignment scenarios across multiple resources
- **Purpose**: Assigns least-privilege RBAC roles to Function App managed identity

**Roles Assigned**:
- Storage Blob Data Contributor
- Storage Queue Data Contributor
- Storage Table Data Contributor
- Cognitive Services User
- Cosmos DB Built-in Data Contributor

## Benefits of Using AVM

### Production Readiness
- Extensively tested across diverse scenarios
- Security best practices built-in
- Regular updates with Azure platform changes

### Standardization
- Consistent parameter naming
- Predictable resource naming patterns
- Uniform tagging strategies

### Maintainability
- Microsoft-maintained with community contributions
- Comprehensive documentation
- Active support channels

### Compliance
- Meets Azure governance standards
- Built-in security configurations
- Audit-ready deployments

## Migration from Custom Modules

If migrating from custom modules (`main.bicep`) to AVM (`main-avm.bicep`):

1. **Backup existing deployment**: Export ARM template or save Bicep files
2. **Review parameter changes**: AVM uses different parameter names
3. **Test in non-production**: Deploy to dev/test first
4. **Validate outputs**: Ensure output names match your dependencies
5. **Update CI/CD**: Modify pipeline to use `main-avm.bicep`

## Additional Resources

- [AVM Official Documentation](https://aka.ms/avm)
- [Bicep Module Registry](https://aka.ms/bicep/modules)
- [Module Contribution Guide](https://azure.github.io/Azure-Verified-Modules/contributing/)
- [AVM GitHub Repository](https://github.com/Azure/Azure-Verified-Modules)

## Support

For issues with AVM modules:
- Check module documentation in the [Bicep Registry](https://github.com/Azure/bicep-registry-modules)
- Open issues in the [AVM GitHub repository](https://github.com/Azure/Azure-Verified-Modules/issues)
- Consult [Microsoft Q&A](https://learn.microsoft.com/answers/tags/454/azure-bicep)
