# FolderToBase64

`FolderToBase64` is a small .NET console tool that:

1. Compresses a folder into a ZIP archive.
2. Converts the ZIP bytes into a Base64 string.
3. Writes that Base64 output to a file.

## Prerequisites

- .NET SDK 8.0 or newer

## Build

```bash
dotnet build
```

## Usage

```bash
dotnet run -- <inputFolderPath> <outputFilePath>
```

Or, after publishing/running the built executable:

```bash
FolderToBase64 <inputFolderPath> <outputFilePath>
```

### Parameters

- `inputFolderPath`: Folder that will be zipped and encoded.
- `outputFilePath`: File path where the Base64 text will be written.

### Example

```bash
dotnet run -- ./my-folder ./output/folder.zip.b64
```

## Helpful hint

If you run the program without parameters (or with invalid parameters), it prints the usage instructions and an example command so you can quickly see how it should be used.
