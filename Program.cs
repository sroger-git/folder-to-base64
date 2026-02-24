#nullable enable
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;

internal static class Program
{
    private const int BufferSize = 1024 * 256;

    private static int Main(string[] args)
    {
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
        {
            PrintUsage();
            return 2;
        }

        string inputFolderPath;
        string outputFilePath;

        try
        {
            inputFolderPath = Path.GetFullPath(args[0]);
            outputFilePath = Path.GetFullPath(args[1]);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Console.Error.WriteLine($"ERROR: Invalid path argument(s): {ex.Message}");
            return 3;
        }

        if (!Directory.Exists(inputFolderPath))
        {
            Console.Error.WriteLine($"ERROR: Input folder does not exist: {inputFolderPath}");
            return 4;
        }

        try
        {
            EnsureOutputDirectory(outputFilePath);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or NotSupportedException or ArgumentException)
        {
            Console.Error.WriteLine($"ERROR: Failed to prepare output directory for '{outputFilePath}': {ex.Message}");
            return 5;
        }

        if (File.Exists(outputFilePath) && IsFileLocked(outputFilePath))
        {
            Console.Error.WriteLine(
                "ERROR: Output file is currently in use (locked) by another process:\n" +
                $"  output: {outputFilePath}\n" +
                "Close any editor/viewer/process using it and try again.");

            return 6;
        }

        var tempZipPath = Path.Combine(Path.GetTempPath(), $"folder-to-base64-{Guid.NewGuid():N}.zip");
        var exitCode = 0;
        var stage = "initializing";

        try
        {
            stage = "creating temporary ZIP";
            ZipFile.CreateFromDirectory(
                sourceDirectoryName: inputFolderPath,
                destinationArchiveFileName: tempZipPath,
                compressionLevel: CompressionLevel.Optimal,
                includeBaseDirectory: true);

            stage = "writing Base64 output file";
            WriteBase64Output(tempZipPath, outputFilePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"ERROR: Access denied during {stage}: {ex.Message}");
            exitCode = 10;
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.Error.WriteLine($"ERROR: Directory not found during {stage}: {ex.Message}");
            exitCode = 11;
        }
        catch (PathTooLongException ex)
        {
            Console.Error.WriteLine($"ERROR: Path too long during {stage}: {ex.Message}");
            exitCode = 12;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"ERROR: Invalid ZIP data during {stage}: {ex.Message}");
            exitCode = 13;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine(BuildDetailedIoError(ex, stage, inputFolderPath, tempZipPath, outputFilePath));
            exitCode = 14;
        }
        catch (NotSupportedException ex)
        {
            Console.Error.WriteLine($"ERROR: Unsupported operation during {stage}: {ex.Message}");
            exitCode = 16;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"ERROR: Invalid argument during {stage}: {ex.Message}");
            exitCode = 15;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: Unexpected {ex.GetType().Name} during {stage}: {ex.Message}");
            exitCode = 99;
        }
        finally
        {
            if (!TryDeleteFile(tempZipPath, out var cleanupError))
            {
                Console.Error.WriteLine(
                    "ERROR: Failed to delete temp ZIP (must not remain on disk):\n" +
                    $"  tempZip: {tempZipPath}\n" +
                    $"  details: {cleanupError}");

                exitCode = exitCode == 0 ? 17 : exitCode;
            }
        }

        return exitCode;
    }

    private static void EnsureOutputDirectory(string outputFilePath)
    {
        var outputDirectory = Path.GetDirectoryName(outputFilePath);

        if (string.IsNullOrEmpty(outputDirectory))
        {
            return;
        }

        Directory.CreateDirectory(outputDirectory);
    }

    private static void WriteBase64Output(string zipPath, string outputFilePath)
    {
        var outputDirectory = Path.GetDirectoryName(outputFilePath) ?? Directory.GetCurrentDirectory();
        var temporaryOutputPath = Path.Combine(outputDirectory, $".{Path.GetFileName(outputFilePath)}.{Guid.NewGuid():N}.tmp");
        var step = "initializing";

        try
        {
            step = "opening temp ZIP for reading";
            using var zipStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan);

            step = "creating temporary output file for Base64";
            using var outputStream = new FileStream(temporaryOutputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize);

            step = "encoding ZIP bytes to Base64";
            using var base64Transform = new ToBase64Transform();
            using var cryptoStream = new CryptoStream(outputStream, base64Transform, CryptoStreamMode.Write, leaveOpen: true);

            zipStream.CopyTo(cryptoStream, BufferSize);
            cryptoStream.FlushFinalBlock();
            outputStream.Flush(flushToDisk: true);

            step = "moving temporary output file into final output path";
            MoveIntoPlaceWithRetries(temporaryOutputPath, outputFilePath);
        }
        catch (IOException ex)
        {
            throw new IOException(
                $"I/O error while {step}.\n" +
                $"  zip: {zipPath}\n" +
                $"  output: {outputFilePath}\n" +
                $"  tempOutput: {temporaryOutputPath}\n" +
                $"  hint: {ExplainIOException(ex)}\n" +
                $"  hresult: 0x{ex.HResult:X8}",
                ex);
        }
        catch
        {
            TryDeleteFile(temporaryOutputPath, out _);
            throw;
        }
        finally
        {
            TryDeleteFile(temporaryOutputPath, out _);
        }
    }

    private static string BuildDetailedIoError(IOException ex, string stage, string inputFolder, string tempZip, string outputFile)
    {
        return
            $"ERROR: I/O error during {stage}.\n" +
            $"  inputFolder: {inputFolder}\n" +
            $"  tempZip: {tempZip}\n" +
            $"  output: {outputFile}\n" +
            $"  hint: {ExplainIOException(ex)}\n" +
            $"  hresult: 0x{ex.HResult:X8}\n" +
            $"  details: {ex.Message}";
    }

    private static string ExplainIOException(IOException ex)
    {
        return ex.HResult switch
        {
            unchecked((int)0x80070020) => "The file is in use (sharing violation). Close any program using the file and retry.",
            unchecked((int)0x80070021) => "The file is locked (lock violation). Another process holds an exclusive lock; retry after it releases.",
            _ => "General I/O failure. It may be a locked file, permission issue, disk issue, or path problem."
        };
    }

    private static bool IsFileLocked(string path)
    {
        try
        {
            using var _ = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static void MoveIntoPlaceWithRetries(string temporaryOutputPath, string outputFilePath)
    {
        const int maxAttempts = 10;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (File.Exists(outputFilePath) && IsFileLocked(outputFilePath))
                {
                    throw new IOException(
                        $"Destination output file is locked (in use by another process): {outputFilePath}",
                        new IOException("Sharing violation") { HResult = unchecked((int)0x80070020) });
                }

                File.Move(temporaryOutputPath, outputFilePath, overwrite: true);
                return;
            }
            catch (IOException ex) when (IsSharingOrLockViolation(ex))
            {
                if (attempt == maxAttempts)
                {
                    throw;
                }

                Thread.Sleep(150 * attempt);
            }
        }
    }

    private static bool IsSharingOrLockViolation(IOException ex)
    {
        const int sharingViolation = unchecked((int)0x80070020);
        const int lockViolation = unchecked((int)0x80070021);

        return ex.HResult is sharingViolation or lockViolation ||
               ex.InnerException is IOException inner && inner.HResult is sharingViolation or lockViolation;
    }

    private static bool TryDeleteFile(string path, out string? errorMessage)
    {
        errorMessage = null;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errorMessage = $"{ex.Message} (hresult=0x{ex.HResult:X8})";
                Thread.Sleep(100 * attempt);
            }
        }

        return false;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Hint: Provide both an input folder and an output file path.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  FolderToBase64 <inputFolderPath> <outputFilePath>");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Example:");
        Console.Error.WriteLine("  FolderToBase64 ./my-folder ./output/folder.zip.b64");
    }
}
