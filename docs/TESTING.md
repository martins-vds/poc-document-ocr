# Testing Guide

This document provides comprehensive information about testing the Document OCR Processor application.

## Test Structure

The test project is located in the `tests/` directory and follows this structure:

```
tests/
├── DocumentOcrProcessor.Tests.csproj   # Test project file
├── README.md                            # Test documentation
├── Services/                            # Service layer tests
│   ├── AiFoundryServiceTests.cs        # AI Foundry service tests
│   └── BlobStorageServiceTests.cs      # Blob storage service tests
└── Models/                              # Model tests
    ├── QueueMessageTests.cs            # Queue message model tests
    ├── DocumentResultTests.cs          # Document result model tests
    └── ProcessingResultTests.cs        # Processing result model tests
```

## Running Tests

### Basic Commands

```bash
# Run all tests
cd tests
dotnet test

# Run tests with normal verbosity
dotnet test --verbosity normal

# Run tests with detailed output
dotnet test --verbosity detailed
```

### Filtering Tests

```bash
# Run tests from a specific class
dotnet test --filter "FullyQualifiedName~AiFoundryServiceTests"

# Run tests matching a pattern
dotnet test --filter "DisplayName~ParseBoundaries"

# Run tests from a specific namespace
dotnet test --filter "FullyQualifiedName~DocumentOcrProcessor.Tests.Services"
```

### Test Coverage

To generate code coverage reports:

```bash
# Install coverlet.msbuild if not already installed
dotnet add package coverlet.msbuild

# Run tests with coverage
dotnet test /p:CollectCoverage=true

# Generate detailed coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Test Categories

### Service Tests

#### AiFoundryServiceTests (13 tests)

Tests the AI Foundry service, particularly the boundary parsing logic:

- **Valid Input Tests**: Validates parsing of comma-separated, space-separated, and newline-separated numbers
- **Edge Case Tests**: Tests handling of duplicates, unsorted numbers, empty strings, and invalid numbers
- **Validation Tests**: Ensures proper error handling for missing configuration

**Key Test Methods:**
- `ParseBoundaries_WithValidCommaSeparatedNumbers_ReturnsCorrectBoundaries`
- `ParseBoundaries_WithDuplicates_RemovesDuplicates`
- `ParseBoundaries_WithUnsortedNumbers_ReturnsSortedList`
- `Constructor_WithMissingEndpoint_ThrowsException`

#### BlobStorageServiceTests (2 tests)

Tests the blob storage service configuration validation:

- **Configuration Tests**: Validates that proper connection string is required

**Key Test Methods:**
- `Constructor_WithMissingConnectionString_ThrowsException`
- `Constructor_WithEmptyConnectionString_ThrowsException`

### Model Tests

#### QueueMessageTests (2 tests)

Tests the queue message model:

- **Initialization Tests**: Validates default values
- **Property Tests**: Ensures all properties can be set correctly

#### DocumentResultTests (2 tests)

Tests the document result model:

- **Initialization Tests**: Validates default collection initialization
- **Property Tests**: Ensures all properties work correctly

#### ProcessingResultTests (3 tests)

Tests the processing result model:

- **Initialization Tests**: Validates default values and timestamp
- **Collection Tests**: Tests document list operations

## Test Frameworks and Tools

- **xUnit 2.9.2**: Main testing framework
- **Moq 4.20.72**: Mocking framework for dependencies
- **Microsoft.NET.Test.Sdk 17.12.0**: Test SDK
- **coverlet.collector 6.0.2**: Code coverage collector

## Writing New Tests

### Test Naming Convention

Follow the pattern: `MethodName_Scenario_ExpectedResult`

Examples:
- `ParseBoundaries_WithValidInput_ReturnsCorrectList`
- `Constructor_WithMissingConfig_ThrowsException`

### Test Structure (AAA Pattern)

```csharp
[Fact]
public void MethodName_Scenario_ExpectedResult()
{
    // Arrange - Set up test data and dependencies
    var mockLogger = new Mock<ILogger<ServiceClass>>();
    var service = new ServiceClass(mockLogger.Object);
    
    // Act - Execute the method being tested
    var result = service.MethodToTest(input);
    
    // Assert - Verify the expected outcome
    Assert.Equal(expectedValue, result);
}
```

### Mocking Dependencies

Use Moq to create mock objects:

```csharp
// Mock a logger
var mockLogger = new Mock<ILogger<MyService>>();

// Mock configuration
var mockConfiguration = new Mock<IConfiguration>();
mockConfiguration.Setup(c => c["Setting:Key"]).Returns("value");

// Use mocks in constructor
var service = new MyService(mockLogger.Object, mockConfiguration.Object);
```

## Continuous Testing

For continuous testing during development, you can use:

```bash
# Watch mode - reruns tests on file changes
dotnet watch test
```

## Best Practices

1. **Test One Thing**: Each test should verify one specific behavior
2. **Use Descriptive Names**: Test names should clearly indicate what they test
3. **Arrange-Act-Assert**: Follow the AAA pattern consistently
4. **Mock External Dependencies**: Use Moq to isolate the unit being tested
5. **Test Edge Cases**: Include tests for boundary conditions and error cases
6. **Keep Tests Fast**: Unit tests should run quickly
7. **Make Tests Independent**: Tests should not depend on each other

## Known Limitations

The current test suite focuses on:
- Business logic in services (particularly parsing logic)
- Configuration validation
- Model initialization

Not currently covered:
- Integration tests with actual Azure services
- End-to-end workflow tests
- PDF manipulation operations (would require test PDF files)

For integration testing with Azure services, consider:
- Using Azure Storage Emulator or Azurite for blob storage
- Mocking Azure AI services or using test endpoints
- Creating dedicated test resources in Azure

## Troubleshooting

### Tests Not Running

```bash
# Clean and rebuild
dotnet clean
dotnet build
dotnet test
```

### Missing Dependencies

```bash
# Restore packages
dotnet restore tests/DocumentOcrProcessor.Tests.csproj
```

### Test Discovery Issues

Ensure:
1. Test classes are public
2. Test methods have `[Fact]` or `[Theory]` attributes
3. Test project references the main project
4. xUnit packages are properly installed

## CI/CD Integration

To integrate tests into a CI/CD pipeline:

```yaml
# Example for GitHub Actions
- name: Run tests
  run: dotnet test --no-build --verbosity normal

# Example for Azure Pipelines
- task: DotNetCoreCLI@2
  displayName: 'Run Tests'
  inputs:
    command: test
    projects: 'tests/**/*.csproj'
```

## Future Improvements

Potential areas for expanding test coverage:

1. **Integration Tests**: Tests that interact with actual Azure services
2. **PDF Processing Tests**: Tests using sample PDF files
3. **Performance Tests**: Validate processing time for large documents
4. **End-to-End Tests**: Complete workflow validation
5. **Contract Tests**: Validate Azure service responses
