# Unified Storage

> Unified cross-platform storage abstractions.

[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

## Description

Unified Storage provides a consistent interface for interacting with storage solutions across different platforms and services. Whether you're working with local files, Azure Blob storage, or Google Drive, Unified Storage simplifies the process with a unified approach.

## Features

- **Cross-Platform**: Designed to work across various platforms.
- **Multiple Storage Backends**: Supports local disk, Azure Blob, Google Drive, and more to come!
- **Easy-to-Use API**: Simplified methods for common storage operations.
- **Integration**: Seamlessly integrate with popular .NET dependency injection frameworks.
- **Extensible**: Easily expand to support additional storage backends.

## Getting Started

### Prerequisites

- .NET SDK
- (Optional) Azure Blob Storage account for AzureBlob integration. Obtain credentials from Azure Portal.
- (Optional) Google Drive API credentials for Google Drive integration. Follow [this guide](https://developers.google.com/drive/api/v3/quickstart/dotnet) to set up credentials.

### Installation

1. Clone this repository:
    ```bash
    git clone https://github.com/chejunkie/unified-storage.git
    ```
2. Navigate to the project directory:
    ```bash
    cd unified-storage
    ```
3. Restore NuGet packages:
    ```bash
    dotnet restore
    ```

## Usage

See the unit test project for in-depth examples. The libraries are designed with Dependency Injection (DI) in mind, but constructor overloads are provided for scenarios without DI.

_For a quick start, refer to the following examples:_

**Local Disk Initialization**
```csharp
// Code snippet...
```

**Azure Blob Initialization**
```csharp
// Code snippet...
```

**Google Drive Initialization**
```csharp
// Code snippet...
```

## Contributing

Contributions are always welcome! If you'd like to contribute:

1. Fork the project.
2. Create a new branch.
3. Make your changes and write tests when practical.
4. Commit your changes to the branch.
5. Push your changes to your fork.
6. Submit a pull request.

## Support

If you encounter any issues or have questions, please open an issue in this repository, and I'll do my best to assist or direct you to the appropriate resource.
