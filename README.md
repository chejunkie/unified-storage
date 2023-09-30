# Unified Storage

> Unified cross-platform storage abstractions.

[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

## Description

Unified Storage provides a consistent interface for interacting with storage solutions across different platforms and services. Whether you're working with local files, Azure Blob storage, or Google Drive, Unified Storage simplifies the process with a unified approach. 

I did not see any solution for my specific use case scenario, so I created these libraries to fit that need. Essentially, I wanted to be able to swap-in different storage solutions so I needed these different backends to share a common interface. It was more of a headache that I thought, mainly because I over-designed the solution, but it works for my needs without paying for a license for alternative solutions. Hopefully some others find it useful too - contributions welcomed! 

## Features

- **Cross-Platform**: Designed to work across various platforms.
- **Multiple Storage Backends**: Supports local disk, Azure Blob, Google Drive, and more to come!
- **Easy-to-Use API**: Simplified methods for common storage operations.
- **Integration**: Seamlessly integrate with popular .NET dependency injection frameworks.
- **Extensible**: Easily expand to support additional storage backends.

## Help Wanted!

My use case is limited in scope, so I cannot think or do everything. If you have ideas or want to contribute additional storage integrations, improvements, or other features, please get involved!

Here's how you can help:

1. **New Storage Providers**: Have experience with other storage solutions? Help integrate them!
2. **Enhancements**: Found a way to improve an existing feature? I'd love to hear about it!
3. **Documentation**: Good at explaining and writing? Because I am not!
4. **Spread the Word**: If you find this library helpful, share it with others, star the repo, and help it grow.

See [Contributing](#contributing) for steps on submitting your contributions.

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
