# Utility Scripts

This directory contains utility scripts for working with PDF files.

## PDF Splitter (`split_pdf.py`)

A Python utility that splits a multi-page PDF into individual single-page PDF files.

### Prerequisites

- Python 3.6 or higher
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

### Usage

Basic usage:

```bash
python split_pdf.py <input_pdf> [output_directory]
```

### Examples

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

### Options

- `input_pdf`: Path to the input PDF file to split (required)
- `output_directory`: Directory where individual page PDFs will be saved (optional, default: `output`)
- `-h, --help`: Show help message
- `-v, --version`: Show version information

### Output

The script creates individual PDF files with the naming pattern:

```
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

### Use Cases

This utility is helpful for:
- Testing the main Document OCR Processor with single-page PDFs
- Preparing test data
- Breaking down large PDFs for individual processing
- Creating sample files for development and testing
