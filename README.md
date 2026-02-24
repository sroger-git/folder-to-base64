# FolderToBase64

`FolderToBase64` is a small C# console toolset with two scripts:

1. `Program.cs`: compresses a folder into ZIP bytes, then writes Base64 text to a file.
2. `base64-to-file.cs`: reads Base64 text from a file and reconstructs the original binary file.

---

## Decode helper (`base64-to-file.cs`) and parameter handling

`base64-to-file.cs` requires **two arguments** after `--` and validates both:

- `arg0`: input Base64 file path
- `arg1`: output file path

Usage:

```bash
dotnet base64-to-file.cs -- <inputBase64File> <outputFile>
```

Example:

```bash
dotnet base64-to-file.cs -- folder-zip.b64 recovered_archive.zip
```

If arguments are missing/invalid, it prints usage and exits with a non-zero code.

---

## If you only have a `.cs` file (no `.csproj`)

Use this flow when your folder contains only a source file such as `Program.cs` or `base64-to-file.cs`.

### Prerequisites

- .NET SDK 8.0+ (`dotnet --version`)

### 1) Create a temporary console project

```bash
dotnet new console -n Runner
cd Runner
```

### 2) Replace generated code with your `.cs` file

```bash
cp ../base64-to-file.cs Program.cs
```

(or copy `../Program.cs` if you want folder -> Base64 behavior)

### 3) Run the tool

```bash
dotnet run -- <inputPath> <outputPath>
```

---

## If you already have this repository (with `.csproj`)

### Encode a folder to Base64 (`Program.cs`)

```bash
dotnet run -- <inputFolderPath> <outputFilePath>
```

Example:

```bash
dotnet run -- "../my-folder" "../output/folder.zip.b64"
```

Parameters:

- `inputFolderPath`: folder that will be zipped and encoded.
- `outputFilePath`: file path where the Base64 text will be written.

### Build first (optional)

```bash
dotnet build
```

---

If you run either program without required parameters (or with invalid parameters), it prints usage instructions and exits with a non-zero exit code.
