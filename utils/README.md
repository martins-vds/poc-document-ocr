# Utility Scripts

This directory contains utility scripts for working with files, PDFs, and JSON data.

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
