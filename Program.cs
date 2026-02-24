#nullable enable
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;

internal static class Program
{
    private const int BufferSize = 1024 * 64;

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
            Console.Error.WriteLine($"ERROR: Failed to prepare output directory: {ex.Message}");
            return 5;
        }

        var tempZipPath = Path.Combine(Path.GetTempPath(), $"folder-to-base64-{Guid.NewGuid():N}.zip");
        var exitCode = 0;

        try
        {
            ZipFile.CreateFromDirectory(
                sourceDirectoryName: inputFolderPath,
                destinationArchiveFileName: tempZipPath,
                compressionLevel: CompressionLevel.Optimal,
                includeBaseDirectory: true);

            WriteBase64Output(tempZipPath, outputFilePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"ERROR: Access denied: {ex.Message}");
            exitCode = 10;
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.Error.WriteLine($"ERROR: Directory not found: {ex.Message}");
            exitCode = 11;
        }
        catch (PathTooLongException ex)
        {
            Console.Error.WriteLine($"ERROR: Path too long: {ex.Message}");
            exitCode = 12;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"ERROR: Invalid ZIP data: {ex.Message}");
            exitCode = 13;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"ERROR: I/O error: {ex.Message}");
            exitCode = 14;
        }
        catch (NotSupportedException ex)
        {
            Console.Error.WriteLine($"ERROR: Unsupported operation: {ex.Message}");
            exitCode = 16;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"ERROR: Invalid argument: {ex.Message}");
            exitCode = 15;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: Unexpected {ex.GetType().Name}: {ex.Message}");
            exitCode = 99;
        }
        finally
        {
            if (!TryDeleteFile(tempZipPath, out var cleanupError))
            {
                Console.Error.WriteLine($"ERROR: Failed to delete temp ZIP '{tempZipPath}': {cleanupError}");
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

        try
        {
            using var zipStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan);
            using var outputStream = new FileStream(temporaryOutputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize);
            using var base64Transform = new ToBase64Transform();
            using var cryptoStream = new CryptoStream(outputStream, base64Transform, CryptoStreamMode.Write, leaveOpen: true);

            zipStream.CopyTo(cryptoStream, BufferSize);
            cryptoStream.FlushFinalBlock();
            outputStream.Flush(flushToDisk: true);

            File.Move(temporaryOutputPath, outputFilePath, overwrite: true);
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
                errorMessage = ex.Message;
                Thread.Sleep(100 * attempt);
            }
        }

        return false;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  FolderToBase64 <inputFolderPath> <outputFilePath>");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Example:");
        Console.Error.WriteLine("  FolderToBase64 ./my-folder ./output/folder.zip.b64");
    }
}
