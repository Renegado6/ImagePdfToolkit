using System.Globalization;
using System.IO;
using PDFtoImage;
using ImagePdfToolkit.Models;

namespace ImagePdfToolkit.Services;

public sealed class PdfImageExtractionService
{
    private const int RenderDpi = 150;

    public static bool IsPdfFile(string path)
        => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);

    public Task<PdfExtractionResult> ExtractAllPagesAsync(
        string pdfPath,
        string selectedOutputDirectory,
        IProgress<PdfExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => ExtractAllPages(pdfPath, selectedOutputDirectory, progress, cancellationToken),
            cancellationToken);
    }

    private static PdfExtractionResult ExtractAllPages(
        string pdfPath,
        string selectedOutputDirectory,
        IProgress<PdfExtractionProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException("The PDF file does not exist.", pdfPath);
        }

        if (!Directory.Exists(selectedOutputDirectory))
        {
            throw new DirectoryNotFoundException("The selected output directory does not exist.");
        }

        var pdfBytes = File.ReadAllBytes(pdfPath);
        cancellationToken.ThrowIfCancellationRequested();
        var pageCount = Conversion.GetPageCount(pdfBytes);
        if (pageCount <= 0)
        {
            throw new InvalidDataException("The PDF does not contain any pages.");
        }

        var outputDirectory = CreateUniqueOutputDirectory(selectedOutputDirectory, pdfPath);
        var numberWidth = Math.Max(3, pageCount.ToString(CultureInfo.InvariantCulture).Length);
        var renderOptions = new RenderOptions(Dpi: RenderDpi);
        progress?.Report(new PdfExtractionProgress(0, pageCount));

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageNumber = pageIndex + 1;
            var outputPath = Path.Combine(
                outputDirectory,
                $"page-{pageNumber.ToString($"D{numberWidth}", CultureInfo.InvariantCulture)}.png");
            Conversion.SavePng(outputPath, pdfBytes, pageIndex, options: renderOptions);
            progress?.Report(new PdfExtractionProgress(pageNumber, pageCount));
        }

        return new PdfExtractionResult(pageCount, outputDirectory);
    }

    private static string CreateUniqueOutputDirectory(string selectedOutputDirectory, string pdfPath)
    {
        var baseName = SanitizeDirectoryName(Path.GetFileNameWithoutExtension(pdfPath));
        var folderName = $"{baseName}_pages";
        var candidate = Path.Combine(selectedOutputDirectory, folderName);
        var suffix = 2;
        while (Directory.Exists(candidate))
        {
            candidate = Path.Combine(selectedOutputDirectory, $"{folderName}_{suffix}");
            suffix++;
        }

        Directory.CreateDirectory(candidate);
        return candidate;
    }

    private static string SanitizeDirectoryName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "pdf" : sanitized;
    }
}
