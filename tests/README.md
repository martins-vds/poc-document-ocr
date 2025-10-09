# Document OCR Processor - Unit Tests

This directory contains unit tests for the Document OCR Processor application.

## Test Coverage

### Services Tests

#### AiFoundryServiceTests
- Tests for the `ParseBoundaries` method which parses AI responses
- Validates handling of various input formats (comma-separated, space-separated, newline-separated)
- Tests edge cases: duplicates, unsorted numbers, invalid numbers, empty strings
- Validates configuration validation in constructor

#### BlobStorageServiceTests
- Tests configuration validation
- Validates that service requires proper connection string

### Models Tests

#### QueueMessageTests
- Tests default values initialization
- Validates property setters

#### DocumentResultTests
- Tests default values initialization
- Validates all properties can be set correctly

#### ProcessingResultTests
- Tests default values initialization
- Validates timestamp is set to UTC
- Tests document collection operations

## Running Tests

```bash
# Run all tests
dotnet test

# Run tests with verbose output
dotnet test --verbosity normal

# Run tests with detailed output
dotnet test --verbosity detailed

# Run specific test class
dotnet test --filter "FullyQualifiedName~AiFoundryServiceTests"
```

## Test Framework

- **xUnit**: Test framework
- **Moq**: Mocking framework for dependencies

## Adding New Tests

When adding new tests:

1. Create test class in the appropriate subdirectory (`Services/`, `Models/`, etc.)
2. Follow the naming convention: `{ClassName}Tests.cs`
3. Use AAA pattern (Arrange, Act, Assert)
4. Name tests descriptively: `MethodName_Scenario_ExpectedResult`

## Coverage Summary

- **Total Tests**: 22
- **Service Tests**: 13
- **Model Tests**: 9

All critical business logic in services and model initialization is covered.
