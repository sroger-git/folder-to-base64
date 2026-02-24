# FolderToBase64

`FolderToBase64` is a small C# console tool that:

1. Compresses a folder into a ZIP archive.
2. Converts the ZIP bytes into a Base64 string.
3. Writes that Base64 output to a file.

---

## If you only have a `.cs` file (no `.csproj`)

Use this flow when your folder contains only a source file such as `base64-to-file.cs` or `Program.cs`.

### Prerequisites

- .NET SDK 8.0+ (`dotnet --version`)

### 1) Create a temporary console project

```bash
dotnet new console -n FolderToBase64Runner
cd FolderToBase64Runner
```

### 2) Replace generated code with your `.cs` file

Copy your source file content into `Program.cs`.

(Example if your file is beside this folder:)

```bash
cp ../base64-to-file.cs Program.cs
```

### 3) Run the tool

```bash
dotnet run -- <inputFolderPath> <outputFilePath>
```

Example:

```bash
dotnet run -- "../my-folder" "../output/folder.zip.b64"
```

---

## If you already have this repository (with `.csproj`)

You can run directly from this repo:

```bash
dotnet run -- <inputFolderPath> <outputFilePath>
```

Or build first:

```bash
dotnet build
dotnet run -- <inputFolderPath> <outputFilePath>
```

### Parameters

- `inputFolderPath`: Folder that will be zipped and encoded.
- `outputFilePath`: File path where the Base64 text will be written.

### Helpful hint

If you run the program without parameters (or with invalid parameters), it prints usage instructions and an example command.
