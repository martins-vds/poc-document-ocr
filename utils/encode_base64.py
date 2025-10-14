#!/usr/bin/env python3
"""
Base64 File Encoder Utility

This script reads a file and outputs its base64-encoded representation to the console.
Useful for encoding files for embedding in JSON, APIs, or other text-based formats.

Usage:
    python encode_base64.py <input_file>

Example:
    python encode_base64.py document.pdf
    python encode_base64.py image.png
    python encode_base64.py /path/to/file.txt
"""

import argparse
import base64
import os
import sys
from pathlib import Path


def encode_file_to_base64(input_path):
    """
    Encode a file to base64 and output to console.
    
    Args:
        input_path (str): Path to the input file
    
    Returns:
        str: Base64-encoded string of the file contents
    """
    # Validate input file
    if not os.path.isfile(input_path):
        print(f"Error: Input file '{input_path}' does not exist.", file=sys.stderr)
        sys.exit(1)
    
    # Check if file is readable
    if not os.access(input_path, os.R_OK):
        print(f"Error: Permission denied reading file '{input_path}'.", file=sys.stderr)
        sys.exit(1)
    
    try:
        # Get file info
        file_size = os.path.getsize(input_path)
        file_name = Path(input_path).name
        
        # Print file information to stderr so it doesn't interfere with base64 output
        print(f"Encoding file: {file_name}", file=sys.stderr)
        print(f"File size: {file_size:,} bytes", file=sys.stderr)
        
        # Read and encode the file
        with open(input_path, 'rb') as file:
            file_content = file.read()
            base64_encoded = base64.b64encode(file_content).decode('utf-8')
        
        # Output the base64 string to stdout
        print(base64_encoded)
        
        # Print completion message to stderr
        encoded_size = len(base64_encoded)
        print(f"Successfully encoded to {encoded_size:,} base64 characters", file=sys.stderr)
        
        return base64_encoded
        
    except PermissionError as e:
        print(f"Error: Permission denied: {str(e)}", file=sys.stderr)
        sys.exit(1)
    except OSError as e:
        print(f"Error reading file: {str(e)}", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"Error processing file: {str(e)}", file=sys.stderr)
        sys.exit(1)


def main():
    """Main entry point for the script."""
    parser = argparse.ArgumentParser(
        description="Encode a file to base64 and output to console.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s document.pdf
  %(prog)s image.png > encoded.txt
  %(prog)s /path/to/file.txt
  
Note: Base64 output goes to stdout, status messages go to stderr.
      Use redirection (>) to save base64 output to a file.
        """
    )
    
    parser.add_argument(
        'input_file',
        help='Path to the input file to encode'
    )
    
    parser.add_argument(
        '-v', '--version',
        action='version',
        version='%(prog)s 1.0.0'
    )
    
    args = parser.parse_args()
    
    # Encode the file
    encode_file_to_base64(args.input_file)


if __name__ == '__main__':
    main()