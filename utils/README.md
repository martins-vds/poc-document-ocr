# Utility Scripts

This directory contains utility scripts for working with files, PDFs, JSON data, and Azure configuration.

## Configuration Settings Updater (`update_settings.py`)

A Python utility to update configuration files for both the Azure Function App and Web App with Azure service credentials. This script simplifies local development setup by updating `local.settings.json` for the Function App and `appsettings.Development.json` for the Web App in one command.

### Prerequisites

- Python 3.8 or higher
- No additional dependencies (uses built-in libraries)

### Usage

**Automatic Mode with azd (Recommended after `azd provision`):**

```bash
python update_settings.py --from-azd-env
```

This mode reads configuration from environment variables set by Azure Developer CLI (azd) postprovision hooks. This is the easiest way to configure local development after running `azd provision`.

**Interactive Mode (Recommended for first-time setup without azd):**

```bash
python update_settings.py --interactive
```

This will prompt you for all required values including:
- Azure Storage connection string
- Document Intelligence endpoint and API key
- Cosmos DB endpoint and key
- Azure AD tenant ID, client ID, and domain

**Command Line Mode (Provide all values):**

```bash
python update_settings.py \
  --storage-connection "DefaultEndpointsProtocol=https;AccountName=mystorageaccount;AccountKey=...;EndpointSuffix=core.windows.net" \
  --doc-intelligence-endpoint "https://myresource.cognitiveservices.azure.com/" \
  --doc-intelligence-key "your-api-key" \
  --cosmosdb-endpoint "https://myaccount.documents.azure.com:443/" \
  --cosmosdb-key "your-cosmosdb-key" \
  --tenant-id "your-tenant-id" \
  --client-id "your-client-id" \
  --domain "yourdomain.onmicrosoft.com"
```

**Local Development Mode (with emulators):**

```bash
python update_settings.py \
  --storage-connection "UseDevelopmentStorage=true" \
  --doc-intelligence-endpoint "https://your-resource.cognitiveservices.azure.com/" \
  --doc-intelligence-key "your-api-key" \
  --cosmosdb-endpoint "https://localhost:8081" \
  --cosmosdb-key "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==" \
  --tenant-id "common" \
  --client-id "your-dev-client-id" \
  --domain "localhost"
```

**Update Function App Settings Only:**

```bash
python update_settings.py --function-only \
  --storage-connection "..." \
  --doc-intelligence-endpoint "..." \
  --doc-intelligence-key "..." \
  --cosmosdb-endpoint "..." \
  --cosmosdb-key "..."
```

**Update Web App Settings Only:**

```bash
python update_settings.py --webapp-only \
  --storage-connection "..." \
  --cosmosdb-endpoint "..." \
  --tenant-id "..." \
  --client-id "..." \
  --domain "..."
```

### Command Options

Required for Function App:
- `--storage-connection`: Azure Storage connection string
- `--doc-intelligence-endpoint`: Document Intelligence endpoint URL (must end with `/`)
- `--doc-intelligence-key`: Document Intelligence API key
- `--cosmosdb-endpoint`: Cosmos DB endpoint URL
- `--cosmosdb-key`: Cosmos DB key

Required for Web App:
- `--storage-connection`: Azure Storage connection string
- `--cosmosdb-endpoint`: Cosmos DB endpoint URL
- `--tenant-id`: Azure AD tenant ID
- `--client-id`: Azure AD client ID (from App Registration)
- `--domain`: Azure AD domain (e.g., `contoso.onmicrosoft.com`)

Optional:
- `--cosmosdb-database`: Cosmos DB database name (default: `DocumentOcrDb`)
- `--cosmosdb-container`: Cosmos DB container name (default: `ProcessedDocuments`)
- `--interactive`, `-i`: Interactive mode (prompts for all values)
- `--function-only`: Update only Function App settings
- `--webapp-only`: Update only Web App settings
- `-h, --help`: Show help message
- `--version`: Show version information

### What It Does

1. **For Azure Function App** (`src/DocumentOcr.Processor/local.settings.json`):
   - Creates the file from template if it doesn't exist
   - Updates Azure Storage connection string
   - Updates Document Intelligence endpoint and API key
   - Updates Cosmos DB endpoint, key, database, and container names
   - Sets FUNCTIONS_WORKER_RUNTIME to `dotnet-isolated`

2. **For Web App** (`src/DocumentOcr.WebApp/appsettings.Development.json`):
   - Creates the file from `appsettings.json` template if it doesn't exist
   - Updates Azure AD configuration for authentication
   - Updates Cosmos DB connection settings
   - Updates Azure Storage connection string
   - Safe for local development (Development settings are in `.gitignore`)

### Output Format

The script outputs status messages to stderr and creates/updates JSON files with proper formatting (2-space indentation). Success messages indicate which files were updated.

### Integration with Azure Developer CLI (azd)

When you run `azd provision`, the postprovision hook automatically:
1. Retrieves connection strings and keys from Azure resources
2. Sets them as environment variables
3. Calls this script with `--from-azd-env` to update local configuration files

This means after running `azd provision`, your local development environment is automatically configured and ready to use!

**Environment variables set by azd postprovision hooks:**
- `AZURE_STORAGE_CONNECTION_STRING`: Storage account connection string
- `AZURE_DOCUMENTINTELLIGENCE_ENDPOINT`: Document Intelligence service endpoint
- `AZURE_DOCUMENTINTELLIGENCE_KEY`: Document Intelligence API key
- `AZURE_COSMOSDB_ENDPOINT`: Cosmos DB endpoint
- `AZURE_COSMOSDB_KEY`: Cosmos DB primary key
- `AZURE_TENANT_ID`: Azure AD tenant ID (from azd env)
- `WEB_APP_CLIENT_ID`: Web app client ID (from azd env)
- `AZURE_AD_DOMAIN`: Azure AD domain (from azd env)

### Use Cases

This utility is essential for:

- **azd Workflow**: Automatically configure local settings after `azd provision`
- **Initial Setup**: Quickly configure both apps for local development
- **Team Onboarding**: Help new developers set up their environment
- **Environment Switching**: Switch between dev/test/prod configurations
- **CI/CD**: Automate configuration updates in deployment pipelines
- **Documentation**: Provide a consistent way to configure the application
- **Error Prevention**: Ensure all required settings are provided correctly

### Security Notes

- Never commit `local.settings.json` or `appsettings.Development.json` with real credentials to source control
- These files are already in `.gitignore`
- The script updates `appsettings.Development.json`, not the base `appsettings.json`, keeping the repository clean
- For production deployments, use Azure Key Vault or Managed Identity instead of keys
- The script is designed for local development only

### Examples

**First-time setup with interactive mode:**
```bash
cd utils
python update_settings.py --interactive
# Follow the prompts to enter your Azure credentials
```

**Quick update for local development:**
```bash
python update_settings.py \
  --storage-connection "UseDevelopmentStorage=true" \
  --doc-intelligence-endpoint "https://eastus.api.cognitive.microsoft.com/" \
  --doc-intelligence-key "abc123..." \
  --cosmosdb-endpoint "https://localhost:8081" \
  --cosmosdb-key "C2y6yDjf5..." \
  --tenant-id "common" \
  --client-id "dev-client-id" \
  --domain "localhost"
```

**Update only Function App for testing:**
```bash
python update_settings.py --function-only \
  --storage-connection "UseDevelopmentStorage=true" \
  --doc-intelligence-endpoint "https://..." \
  --doc-intelligence-key "..." \
  --cosmosdb-endpoint "https://..." \
  --cosmosdb-key "..."
```

## Base64 File Encoder (`encode_base64.py`)

A Python utility that reads any file and outputs its base64-encoded representation to the console.

### Prerequisites

- Python 3.8 or higher
- No additional dependencies (uses built-in libraries)

### Usage

Basic usage:

```bash
python encode_base64.py <input_file>
```

### Examples

Encode a PDF file:

```bash
python encode_base64.py document.pdf
```

Encode an image file:

```bash
python encode_base64.py image.png
```

Save base64 output to a file:

```bash
python encode_base64.py document.pdf > encoded_document.txt
```

Encode with full path:

```bash
python encode_base64.py /path/to/file.txt
```

### Command Options

- `input_file`: Path to the input file to encode (required)
- `-h, --help`: Show help message
- `-v, --version`: Show version information

### Output Format

The script outputs the base64-encoded string to stdout (console), while status messages are sent to stderr. This allows you to:

- View the base64 string directly in the terminal
- Redirect the base64 output to a file using `> filename.txt`
- Use the output in pipes with other commands

### Use Cases

This utility is helpful for:

- Encoding files for API requests that require base64 data
- Embedding binary files in JSON or XML
- Testing the Document OCR Processor with base64-encoded PDFs
- Converting files for web applications or data URIs
- Preparing test data for applications that expect base64 input

## JSON Schema Generator (`generate_json_schema.py`)

A Python utility that reads a JSON file and generates a JSON schema based on the structure and data types found in the JSON data. This utility uses the `genson` library for robust and professional schema generation.

### Schema Prerequisites

- Python 3.8 or higher
- genson library

### Schema Installation

Install the required Python dependencies:

```bash
pip install -r requirements.txt
```

Or install genson directly:

```bash
pip install genson
```

### Schema Usage

Basic usage:

```bash
python generate_json_schema.py <input_json_file>
```

### Schema Examples

Generate schema for a JSON file:

```bash
python generate_json_schema.py data.json
```

Generate schema for configuration file:

```bash
python generate_json_schema.py config.json
```

Save schema output to a file:

```bash
python generate_json_schema.py data.json > schema.json
```

Generate schema with full path:

```bash
python generate_json_schema.py /path/to/file.json
```

### Schema Command Options

- `input_json_file`: Path to the input JSON file to analyze (required)
- `-h, --help`: Show help message
- `-v, --version`: Show version information

### Schema Output Format

The script outputs the JSON schema to stdout (console), while status messages are sent to stderr. The generated schema follows the JSON Schema Draft 7 specification and includes:

- **Advanced Type Inference**: Automatically detects and properly handles all JSON types
- **Schema Merging**: Combines multiple examples to create comprehensive schemas
- **Optional Properties**: Intelligently determines which properties are required vs optional
- **Complex Structures**: Handles deeply nested objects and arrays with mixed types
- **Pattern Recognition**: Identifies common patterns and constraints in the data

### Genson Library Features

The utility leverages the `genson` library which provides:

- **Robust Type Detection**: More accurate than manual type inference
- **Schema Optimization**: Generates clean, minimal schemas without redundancy
- **Standards Compliance**: Full JSON Schema Draft 7 specification support
- **Performance**: Efficient processing of large JSON files
- **Flexibility**: Handles edge cases and complex data structures gracefully

### Schema Use Cases

This utility is helpful for:

- **API Documentation**: Creating precise schema definitions for REST APIs
- **Data Validation**: Generating validation rules for JSON configuration files
- **Testing Frameworks**: Creating schemas for automated JSON data validation
- **Documentation**: Generating comprehensive data structure documentation
- **Integration**: Helping other systems understand JSON data formats
- **Azure Function Development**: Validating input/output schemas for the Document OCR Processor

## PDF Splitter (`split_pdf.py`)

A Python utility that splits a multi-page PDF into individual single-page PDF files.

### Installation Prerequisites

- Python 3.8 or higher
- pypdf library

### Installation

Install the required Python dependencies:

```bash
pip install -r requirements.txt
```

Or install pypdf directly:

```bash
pip install pypdf
```

### PDF Splitter Usage

Basic usage:

```bash
python split_pdf.py <input_pdf> [output_directory]
```

### PDF Splitter Examples

Split a PDF into the default `output` directory:

```bash
python split_pdf.py document.pdf
```

Split a PDF into a specific directory:

```bash
python split_pdf.py document.pdf my_pages/
```

Split a PDF with full path:

```bash
python split_pdf.py /path/to/multi-page.pdf /path/to/output/
```

### PDF Splitter Options

- `input_pdf`: Path to the input PDF file to split (required)
- `output_directory`: Directory where individual page PDFs will be saved (optional, default: `output`)
- `-h, --help`: Show help message
- `-v, --version`: Show version information

### PDF Splitter Output

The script creates individual PDF files with the naming pattern:

```text
{original_filename}_page_{page_number}.pdf
```

For example, splitting `document.pdf` with 3 pages creates:

- `document_page_0001.pdf`
- `document_page_0002.pdf`
- `document_page_0003.pdf`

Page numbers are zero-padded to 4 digits for proper sorting.

### Error Handling

The script includes error handling for:

- Missing input file
- Invalid PDF files
- Permission issues
- Missing pypdf library

### PDF Splitter Use Cases

This utility is helpful for:

- Testing the main Document OCR Processor with single-page PDFs
- Preparing test data
- Breaking down large PDFs for individual processing
- Creating sample files for development and testing
