#!/usr/bin/env python3
"""
Update Azure Configuration Settings

A utility script to update configuration files for the Document OCR Processor.
Updates both the Azure Function local.settings.json and Web App appsettings.json
with Azure service credentials.

Usage:
    python update_settings.py [--interactive] [--storage-connection <conn>] [...]
    python update_settings.py --help

Note:
    - Updates local.settings.json for Azure Function App
    - Updates appsettings.Development.json for Web App (never commits secrets)

Examples:
    # Interactive mode (prompts for all values)
    python update_settings.py --interactive

    # Provide all values via command line
    python update_settings.py \
        --storage-connection "DefaultEndpointsProtocol=https;AccountName=..." \
        --doc-intelligence-endpoint "https://your-resource.cognitiveservices.azure.com/" \
        --doc-intelligence-key "your-api-key" \
        --cosmosdb-endpoint "https://your-account.documents.azure.com:443/" \
        --cosmosdb-key "your-key" \
        --tenant-id "your-tenant-id" \
        --client-id "your-client-id" \
        --domain "your-domain.onmicrosoft.com"

    # Provide some values, prompt for others
    python update_settings.py \
        --storage-connection "..." \
        --interactive
"""

import argparse
import json
import os
import sys
from pathlib import Path
from typing import Dict, Optional


VERSION = "1.0.0"


def get_project_root() -> Path:
    """Find the project root directory."""
    utils_dir = Path(__file__).resolve().parent
    # Go up from utils/ to project root
    return utils_dir.parent


def get_function_settings_path() -> Path:
    """Get path to Function App local.settings.json."""
    root = get_project_root()
    return root / "src" / "DocumentOcrProcessor" / "local.settings.json"


def get_function_template_path() -> Path:
    """Get path to Function App local.settings.json.template."""
    root = get_project_root()
    return root / "src" / "DocumentOcrProcessor" / "local.settings.json.template"


def get_webapp_settings_path() -> Path:
    """Get path to Web App appsettings.Development.json."""
    root = get_project_root()
    return root / "src" / "DocumentOcrWebApp" / "appsettings.Development.json"


def get_webapp_template_path() -> Path:
    """Get path to Web App appsettings.Development.json.template."""
    root = get_project_root()
    return root / "src" / "DocumentOcrWebApp" / "appsettings.Development.json.template"


def prompt_for_value(prompt_text: str, default: str = "", required: bool = True) -> str:
    """Prompt user for a value with optional default."""
    if default:
        prompt_text = f"{prompt_text} [{default}]: "
    else:
        prompt_text = f"{prompt_text}: "
    
    while True:
        value = input(prompt_text).strip()
        if not value and default:
            return default
        if not value and required:
            print("Error: This value is required.", file=sys.stderr)
            continue
        return value


def load_json_file(file_path: Path) -> Dict:
    """Load and parse a JSON file."""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            return json.load(f)
    except FileNotFoundError:
        print(f"Error: File not found: {file_path}", file=sys.stderr)
        sys.exit(1)
    except json.JSONDecodeError as e:
        print(f"Error: Invalid JSON in {file_path}: {e}", file=sys.stderr)
        sys.exit(1)


def save_json_file(file_path: Path, data: Dict) -> None:
    """Save data to a JSON file with proper formatting."""
    try:
        with open(file_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2)
            f.write('\n')  # Add newline at end of file
        print(f"✓ Updated: {file_path}", file=sys.stderr)
    except Exception as e:
        print(f"Error: Failed to write {file_path}: {e}", file=sys.stderr)
        sys.exit(1)


def update_function_settings(
    storage_connection: str,
    doc_intelligence_endpoint: str,
    doc_intelligence_key: str,
    cosmosdb_endpoint: str,
    cosmosdb_key: str,
    cosmosdb_database: str = "DocumentOcrDb",
    cosmosdb_container: str = "ProcessedDocuments"
) -> None:
    """Update Azure Function local.settings.json."""
    settings_path = get_function_settings_path()
    template_path = get_function_template_path()
    
    # Validate and fix Document Intelligence endpoint URL
    if not doc_intelligence_endpoint.endswith('/'):
        print("Warning: Document Intelligence endpoint should end with '/' - adding it automatically", file=sys.stderr)
        doc_intelligence_endpoint = doc_intelligence_endpoint + '/'
    
    # Validate and fix Cosmos DB endpoint URL
    if not cosmosdb_endpoint.endswith('/'):
        print("Warning: Cosmos DB endpoint should end with '/' - adding it automatically", file=sys.stderr)
        cosmosdb_endpoint = cosmosdb_endpoint + '/'
    
    # Load template or existing settings
    if settings_path.exists():
        print(f"Loading existing settings from: {settings_path}", file=sys.stderr)
        settings = load_json_file(settings_path)
    elif template_path.exists():
        print(f"Creating settings from template: {template_path}", file=sys.stderr)
        settings = load_json_file(template_path)
    else:
        print("Creating new settings file", file=sys.stderr)
        settings = {
            "IsEncrypted": False,
            "Values": {}
        }
    
    # Ensure Values section exists
    if "Values" not in settings:
        settings["Values"] = {}
    
    # Update settings
    settings["Values"]["AzureWebJobsStorage"] = storage_connection
    settings["Values"]["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated"
    settings["Values"]["DocumentIntelligence:Endpoint"] = doc_intelligence_endpoint
    settings["Values"]["DocumentIntelligence:ApiKey"] = doc_intelligence_key
    settings["Values"]["CosmosDb:Endpoint"] = cosmosdb_endpoint
    settings["Values"]["CosmosDb:Key"] = cosmosdb_key
    settings["Values"]["CosmosDb:DatabaseName"] = cosmosdb_database
    settings["Values"]["CosmosDb:ContainerName"] = cosmosdb_container
    
    save_json_file(settings_path, settings)


def update_webapp_settings(
    storage_connection: str,
    cosmosdb_endpoint: str,
    cosmosdb_key: Optional[str],
    cosmosdb_database: str,
    cosmosdb_container: str,
    tenant_id: str,
    client_id: str,
    domain: str
) -> None:
    """Update Web App appsettings.Development.json."""
    settings_path = get_webapp_settings_path()
    template_path = get_webapp_template_path()
    
    # Validate and fix Cosmos DB endpoint URL
    if not cosmosdb_endpoint.endswith('/'):
        print("Warning: Cosmos DB endpoint should end with '/' - adding it automatically", file=sys.stderr)
        cosmosdb_endpoint = cosmosdb_endpoint + '/'
    
    # Load existing settings or create from template
    if settings_path.exists():
        print(f"Loading existing settings from: {settings_path}", file=sys.stderr)
        settings = load_json_file(settings_path)
    elif template_path.exists():
        print(f"Creating Development settings from template: {template_path}", file=sys.stderr)
        settings = load_json_file(template_path)
    else:
        # Fall back to appsettings.json
        fallback_path = settings_path.parent / "appsettings.json"
        if fallback_path.exists():
            print(f"Creating Development settings from: {fallback_path}", file=sys.stderr)
            settings = load_json_file(fallback_path)
        else:
            print("Creating new settings file", file=sys.stderr)
            settings = {
                "Logging": {
                    "LogLevel": {
                        "Default": "Information",
                        "Microsoft.AspNetCore": "Warning"
                    }
                }
            }
    
    # Update Azure AD configuration
    if "AzureAd" not in settings:
        settings["AzureAd"] = {}
    
    settings["AzureAd"]["Instance"] = "https://login.microsoftonline.com/"
    settings["AzureAd"]["Domain"] = domain
    settings["AzureAd"]["TenantId"] = tenant_id
    settings["AzureAd"]["ClientId"] = client_id
    settings["AzureAd"]["CallbackPath"] = "/signin-oidc"
    
    # Update Cosmos DB configuration
    if "CosmosDb" not in settings:
        settings["CosmosDb"] = {}
    
    settings["CosmosDb"]["Endpoint"] = cosmosdb_endpoint
    if cosmosdb_key:
        # Note: In production, use Managed Identity instead of keys
        # This is for local development only
        settings["CosmosDb"]["Key"] = cosmosdb_key
    settings["CosmosDb"]["DatabaseName"] = cosmosdb_database
    settings["CosmosDb"]["ContainerName"] = cosmosdb_container
    
    # Update storage connection
    settings["AzureWebJobsStorage"] = storage_connection
    
    save_json_file(settings_path, settings)


def interactive_mode() -> Dict[str, str]:
    """Prompt user for all required configuration values."""
    print("\n=== Azure Function & Web App Configuration ===", file=sys.stderr)
    print("Enter your Azure service credentials.\n", file=sys.stderr)
    
    config = {}
    
    print("Storage Account:", file=sys.stderr)
    config["storage_connection"] = prompt_for_value(
        "  Connection String",
        default="UseDevelopmentStorage=true"
    )
    
    print("\nDocument Intelligence:", file=sys.stderr)
    config["doc_intelligence_endpoint"] = prompt_for_value(
        "  Endpoint (must end with /)",
        default="https://your-resource.cognitiveservices.azure.com/"
    )
    config["doc_intelligence_key"] = prompt_for_value(
        "  API Key",
        default="your-api-key"
    )
    
    print("\nCosmos DB:", file=sys.stderr)
    config["cosmosdb_endpoint"] = prompt_for_value(
        "  Endpoint",
        default="https://your-account.documents.azure.com:443/"
    )
    config["cosmosdb_key"] = prompt_for_value(
        "  Key",
        default="your-cosmosdb-key"
    )
    config["cosmosdb_database"] = prompt_for_value(
        "  Database Name",
        default="DocumentOcrDb"
    )
    config["cosmosdb_container"] = prompt_for_value(
        "  Container Name",
        default="ProcessedDocuments"
    )
    
    print("\nAzure AD (for Web App):", file=sys.stderr)
    config["tenant_id"] = prompt_for_value(
        "  Tenant ID",
        default="your-tenant-id"
    )
    config["client_id"] = prompt_for_value(
        "  Client ID",
        default="your-client-id"
    )
    config["domain"] = prompt_for_value(
        "  Domain",
        default="your-domain.onmicrosoft.com"
    )
    
    return config


def main():
    """Main entry point."""
    parser = argparse.ArgumentParser(
        description="Update Azure configuration settings for Function App and Web App",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Interactive mode
  python update_settings.py --interactive

  # Provide all values
  python update_settings.py \\
    --storage-connection "DefaultEndpointsProtocol=https;..." \\
    --doc-intelligence-endpoint "https://..." \\
    --doc-intelligence-key "key" \\
    --cosmosdb-endpoint "https://..." \\
    --cosmosdb-key "key" \\
    --tenant-id "id" \\
    --client-id "id" \\
    --domain "domain.onmicrosoft.com"

For local development:
  python update_settings.py \\
    --storage-connection "UseDevelopmentStorage=true" \\
    --cosmosdb-endpoint "https://localhost:8081" \\
    --interactive
        """
    )
    
    parser.add_argument('--version', action='version', version=f'%(prog)s {VERSION}')
    parser.add_argument(
        '--interactive', '-i',
        action='store_true',
        help='Interactive mode: prompt for all values'
    )
    parser.add_argument(
        '--storage-connection',
        help='Azure Storage connection string'
    )
    parser.add_argument(
        '--doc-intelligence-endpoint',
        help='Document Intelligence endpoint URL (trailing slash will be added if missing)'
    )
    parser.add_argument(
        '--doc-intelligence-key',
        help='Document Intelligence API key'
    )
    parser.add_argument(
        '--cosmosdb-endpoint',
        help='Cosmos DB endpoint URL (trailing slash will be added if missing)'
    )
    parser.add_argument(
        '--cosmosdb-key',
        help='Cosmos DB key (optional if using Managed Identity)'
    )
    parser.add_argument(
        '--cosmosdb-database',
        default='DocumentOcrDb',
        help='Cosmos DB database name (default: DocumentOcrDb)'
    )
    parser.add_argument(
        '--cosmosdb-container',
        default='ProcessedDocuments',
        help='Cosmos DB container name (default: ProcessedDocuments)'
    )
    parser.add_argument(
        '--tenant-id',
        help='Azure AD tenant ID for web app'
    )
    parser.add_argument(
        '--client-id',
        help='Azure AD client ID for web app'
    )
    parser.add_argument(
        '--domain',
        help='Azure AD domain for web app'
    )
    parser.add_argument(
        '--function-only',
        action='store_true',
        help='Update only the Function App settings'
    )
    parser.add_argument(
        '--webapp-only',
        action='store_true',
        help='Update only the Web App settings'
    )
    
    args = parser.parse_args()
    
    # Gather configuration
    if args.interactive:
        config = interactive_mode()
    else:
        # Use command line arguments
        config = {
            'storage_connection': args.storage_connection,
            'doc_intelligence_endpoint': args.doc_intelligence_endpoint,
            'doc_intelligence_key': args.doc_intelligence_key,
            'cosmosdb_endpoint': args.cosmosdb_endpoint,
            'cosmosdb_key': args.cosmosdb_key,
            'cosmosdb_database': args.cosmosdb_database,
            'cosmosdb_container': args.cosmosdb_container,
            'tenant_id': args.tenant_id,
            'client_id': args.client_id,
            'domain': args.domain,
        }
        
        # Validate required arguments for non-interactive mode
        if not args.webapp_only:
            required_function = ['storage_connection', 'doc_intelligence_endpoint', 
                               'doc_intelligence_key', 'cosmosdb_endpoint', 'cosmosdb_key']
            missing = [k for k in required_function if not config.get(k)]
            if missing:
                parser.error(f"Missing required arguments for Function App: {', '.join('--' + k.replace('_', '-') for k in missing)}")
        
        if not args.function_only:
            required_webapp = ['storage_connection', 'cosmosdb_endpoint', 
                             'tenant_id', 'client_id', 'domain']
            missing = [k for k in required_webapp if not config.get(k)]
            if missing:
                parser.error(f"Missing required arguments for Web App: {', '.join('--' + k.replace('_', '-') for k in missing)}")
    
    print("\n=== Updating Configuration Files ===\n", file=sys.stderr)
    
    # Update Function App settings
    if not args.webapp_only:
        try:
            update_function_settings(
                storage_connection=config['storage_connection'],
                doc_intelligence_endpoint=config['doc_intelligence_endpoint'],
                doc_intelligence_key=config['doc_intelligence_key'],
                cosmosdb_endpoint=config['cosmosdb_endpoint'],
                cosmosdb_key=config['cosmosdb_key'],
                cosmosdb_database=config['cosmosdb_database'],
                cosmosdb_container=config['cosmosdb_container']
            )
        except Exception as e:
            print(f"Error updating Function App settings: {e}", file=sys.stderr)
            sys.exit(1)
    
    # Update Web App settings
    if not args.function_only:
        try:
            update_webapp_settings(
                storage_connection=config['storage_connection'],
                cosmosdb_endpoint=config['cosmosdb_endpoint'],
                cosmosdb_key=config.get('cosmosdb_key'),
                cosmosdb_database=config['cosmosdb_database'],
                cosmosdb_container=config['cosmosdb_container'],
                tenant_id=config['tenant_id'],
                client_id=config['client_id'],
                domain=config['domain']
            )
        except Exception as e:
            print(f"Error updating Web App settings: {e}", file=sys.stderr)
            sys.exit(1)
    
    print("\n=== Configuration Updated Successfully ===", file=sys.stderr)
    print("\nNext steps:", file=sys.stderr)
    if not args.webapp_only:
        print("  1. Review: src/DocumentOcrProcessor/local.settings.json", file=sys.stderr)
    if not args.function_only:
        print("  2. Review: src/DocumentOcrWebApp/appsettings.Development.json", file=sys.stderr)
    print("  3. Start developing with: func start (for Function) or dotnet run (for Web App)", file=sys.stderr)
    print("\n")


if __name__ == '__main__':
    main()
