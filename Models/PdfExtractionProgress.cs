namespace ImagePdfToolkit.Models;

public sealed record PdfExtractionProgress(int CompletedPages, int TotalPages);

public sealed record PdfExtractionResult(int PageCount, string OutputDirectory);
