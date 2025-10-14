# Utility Scripts

This directory contains utility scripts for working with files and PDFs.

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
