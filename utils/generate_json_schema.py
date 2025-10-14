#!/usr/bin/env python3
"""
JSON Schema Generator Utility

This script reads a JSON file and generates a JSON schema based on the structure
and data types found in the JSON data. The schema follows the JSON Schema Draft 7 specification.

This utility uses the genson library for robust schema generation with advanced features
like schema merging, type inference, and proper handling of complex data structures.

Usage:
    python generate_json_schema.py <input_json_file>

Example:
    python generate_json_schema.py data.json
    python generate_json_schema.py config.json > schema.json
    python generate_json_schema.py /path/to/file.json
"""

import argparse
import json
import os
import sys
from pathlib import Path

try:
    from genson import SchemaBuilder
except ImportError:
    print("Error: genson library is not installed.", file=sys.stderr)
    print("Please install it using: pip install genson", file=sys.stderr)
    print("Or install all requirements: pip install -r requirements.txt", file=sys.stderr)
    sys.exit(1)


def generate_json_schema(json_data, title=None, description=None):
    """
    Generate a JSON schema from JSON data using genson.
    
    Args:
        json_data: The parsed JSON data
        title: Optional title for the schema
        description: Optional description for the schema
        
    Returns:
        dict: The generated JSON schema
    """
    # Create a SchemaBuilder instance
    builder = SchemaBuilder()
    
    # Add the JSON data to the builder
    # genson can handle single objects or arrays of objects
    if isinstance(json_data, list):
        # For arrays, add each item to build a comprehensive schema
        for item in json_data:
            builder.add_object(item)
    else:
        # For single objects
        builder.add_object(json_data)
    
    # Generate the schema
    schema = builder.to_schema()
    
    # Add custom metadata if provided
    if title:
        schema["title"] = title
    else:
        schema["title"] = "Generated JSON Schema"
    
    if description:
        schema["description"] = description
    else:
        schema["description"] = "Schema generated from sample JSON data using genson"
    
    # Add schema version if not present
    if "$schema" not in schema:
        schema["$schema"] = "https://json-schema.org/draft-07/schema#"
    
    # Add an ID if not present
    if "$id" not in schema:
        schema["$id"] = "generated-schema"
    
    return schema


def generate_schema_from_file(input_path):
    """
    Generate JSON schema from a JSON file.
    
    Args:
        input_path: Path to the input JSON file
        
    Returns:
        dict: The generated JSON schema
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
        
        # Print file information to stderr
        print(f"Analyzing JSON file: {file_name}", file=sys.stderr)
        print(f"File size: {file_size:,} bytes", file=sys.stderr)
        
        # Read and parse the JSON file
        with open(input_path, 'r', encoding='utf-8') as file:
            json_data = json.load(file)
        
        # Generate custom title and description based on filename
        base_name = Path(input_path).stem
        title = f"Schema for {base_name}"
        description = f"JSON schema generated from {file_name} using genson library"
        
        # Generate the schema
        schema = generate_json_schema(json_data, title=title, description=description)
        
        # Output the schema to stdout
        print(json.dumps(schema, indent=2, ensure_ascii=False))
        
        # Print completion message to stderr
        print(f"Successfully generated JSON schema using genson", file=sys.stderr)
        
        return schema
        
    except json.JSONDecodeError as e:
        print(f"Error: Invalid JSON file: {str(e)}", file=sys.stderr)
        sys.exit(1)
    except PermissionError as e:
        print(f"Error: Permission denied: {str(e)}", file=sys.stderr)
        sys.exit(1)
    except UnicodeDecodeError as e:
        print(f"Error: File encoding issue: {str(e)}", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"Error processing JSON file: {str(e)}", file=sys.stderr)
        sys.exit(1)


def main():
    """Main entry point for the script."""
    parser = argparse.ArgumentParser(
        description="Generate a JSON schema from a JSON file using genson library.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s data.json
  %(prog)s config.json > schema.json
  %(prog)s /path/to/file.json
  
Note: JSON schema output goes to stdout, status messages go to stderr.
      Use redirection (>) to save schema output to a file.
      
This utility uses the genson library for robust schema generation with features like:
- Advanced type inference and schema merging
- Proper handling of optional properties
- Support for complex nested structures
- JSON Schema Draft 7 compliance
        """
    )
    
    parser.add_argument(
        'input_json_file',
        help='Path to the input JSON file to analyze'
    )
    
    parser.add_argument(
        '-v', '--version',
        action='version',
        version='%(prog)s 2.0.0'
    )
    
    args = parser.parse_args()
    
    # Generate the schema
    generate_schema_from_file(args.input_json_file)


if __name__ == '__main__':
    main()