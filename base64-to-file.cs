#nullable enable
using System.Security.Cryptography;
using System.Threading;

internal static class Program
{
    private const int BufferSize = 1024 * 1024;

    public static int Main(string[] args)
    {
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
        {
            PrintUsage();
            return 2;
        }

        string inputBase64Path;
        string outputFilePath;

        try
        {
            inputBase64Path = Path.GetFullPath(args[0]);
            outputFilePath = Path.GetFullPath(args[1]);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Console.Error.WriteLine($"ERROR: Invalid path argument(s): {ex.Message}");
            return 3;
        }

        if (!File.Exists(inputBase64Path))
        {
            Console.Error.WriteLine($"ERROR: Input Base64 file does not exist: {inputBase64Path}");
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

        var outputDirectory = Path.GetDirectoryName(outputFilePath) ?? Directory.GetCurrentDirectory();
        var tempOutPath = Path.Combine(outputDirectory, $".{Path.GetFileName(outputFilePath)}.{Guid.NewGuid():N}.tmp");
        var exitCode = 0;

        try
        {
            using var inputStream = new FileStream(
                inputBase64Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.SequentialScan);

            using var outputStream = new FileStream(
                tempOutPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.SequentialScan);

            using var transform = new FromBase64Transform(FromBase64TransformMode.IgnoreWhiteSpaces);
            using var cryptoStream = new CryptoStream(inputStream, transform, CryptoStreamMode.Read);

            cryptoStream.CopyTo(outputStream, BufferSize);
            outputStream.Flush(flushToDisk: true);
            File.Move(tempOutPath, outputFilePath, overwrite: true);

            Console.Error.WriteLine($"Success! File saved as: {outputFilePath}");
        }
        catch (CryptographicException ex)
        {
            Console.Error.WriteLine($"ERROR: Base64 decode failed (invalid Base64?): {ex.Message}");
            exitCode = 13;
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
            TryDeleteFile(tempOutPath);
        }

        return exitCode;
    }

    private static void EnsureOutputDirectory(string outputFilePath)
    {
        var directory = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void TryDeleteFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100 * attempt);
            }
        }
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  dotnet base64-to-file.cs -- <inputBase64File> <outputFile>");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Example:");
        Console.Error.WriteLine("  dotnet base64-to-file.cs -- folder.zip.b64 recovered_archive.zip");
    }
}
