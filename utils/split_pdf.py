#!/usr/bin/env python3
"""
PDF Splitter Utility

This script splits a multi-page PDF into individual single-page PDF files.
Each page is saved as a separate PDF file in the specified output directory.

Usage:
    python split_pdf.py <input_pdf> [output_directory]

Example:
    python split_pdf.py document.pdf output_pages/
    python split_pdf.py document.pdf  # Uses default 'output' directory
"""

import argparse
import os
import sys
from pathlib import Path


def split_pdf(input_path, output_dir):
    """
    Split a PDF file into individual pages.
    
    Args:
        input_path (str): Path to the input PDF file
        output_dir (str): Directory where individual page PDFs will be saved
    
    Returns:
        int: Number of pages extracted
    """
    try:
        from pypdf import PdfReader, PdfWriter
    except ImportError:
        print("Error: pypdf library is not installed.")
        print("Please install it using: pip install pypdf")
        sys.exit(1)
    
    # Validate input file
    if not os.path.isfile(input_path):
        print(f"Error: Input file '{input_path}' does not exist.")
        sys.exit(1)
    
    # Create output directory if it doesn't exist
    os.makedirs(output_dir, exist_ok=True)
    
    # Get base filename without extension
    base_filename = Path(input_path).stem
    
    try:
        # Read the PDF
        print(f"Reading PDF: {input_path}")
        reader = PdfReader(input_path)
        total_pages = len(reader.pages)
        print(f"Total pages: {total_pages}")
        
        # Extract each page
        for page_num in range(total_pages):
            # Create a new PDF writer for this page
            writer = PdfWriter()
            
            # Add the page to the writer
            writer.add_page(reader.pages[page_num])
            
            # Generate output filename with zero-padded page number
            output_filename = f"{base_filename}_page_{page_num + 1:04d}.pdf"
            output_path = os.path.join(output_dir, output_filename)
            
            # Write the page to a new PDF file
            with open(output_path, 'wb') as output_file:
                writer.write(output_file)
            
            print(f"  Extracted page {page_num + 1}/{total_pages} -> {output_filename}")
        
        print(f"\nSuccessfully split {total_pages} pages into '{output_dir}'")
        return total_pages
        
    except Exception as e:
        print(f"Error processing PDF: {str(e)}")
        sys.exit(1)


def main():
    """Main entry point for the script."""
    parser = argparse.ArgumentParser(
        description="Split a multi-page PDF into individual single-page PDF files.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s document.pdf
  %(prog)s document.pdf my_output_folder/
  %(prog)s /path/to/multi-page.pdf /path/to/output/
        """
    )
    
    parser.add_argument(
        'input_pdf',
        help='Path to the input PDF file to split'
    )
    
    parser.add_argument(
        'output_directory',
        nargs='?',
        default='output',
        help='Directory where individual page PDFs will be saved (default: output)'
    )
    
    parser.add_argument(
        '-v', '--version',
        action='version',
        version='%(prog)s 1.0.0'
    )
    
    args = parser.parse_args()
    
    # Split the PDF
    split_pdf(args.input_pdf, args.output_directory)


if __name__ == '__main__':
    main()
