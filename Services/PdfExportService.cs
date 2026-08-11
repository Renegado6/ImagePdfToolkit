using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;

namespace RandomWatermarkTool.Services;

public sealed class PdfExportService
{
    private readonly ImageProcessingService _imageService;

    public PdfExportService(ImageProcessingService imageService)
    {
        _imageService = imageService;
    }

    public IReadOnlyList<string> GetOrderedSourceImages(string directory)
    {
        return Directory
            .EnumerateFiles(directory)
            .Where(IsPdfSourceImage)
            .OrderBy(path => path, ImageFileNameComparer.Instance)
            .ToArray();
    }

    public void WriteImagesToPdf(IReadOnlyList<string> imagePaths, string pdfPath)
    {
        using var stream = new FileStream(pdfPath, FileMode.Create, FileAccess.Write, FileShare.None);
        var objectCount = 2 + imagePaths.Count * 3;
        var offsets = new long[objectCount + 1];

        WriteAscii(stream, "%PDF-1.4\n");
        WriteObject(stream, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>\n");
        var kids = string.Join(" ", Enumerable.Range(0, imagePaths.Count).Select(index => $"{3 + index * 3} 0 R"));
        WriteObject(stream, offsets, 2, $"<< /Type /Pages /Count {imagePaths.Count} /Kids [{kids}] >>\n");

        for (var index = 0; index < imagePaths.Count; index++)
        {
            var pdfImage = LoadPdfImage(imagePaths[index]);
            var pageObjectId = 3 + index * 3;
            var contentObjectId = pageObjectId + 1;
            var imageObjectId = pageObjectId + 2;
            var imageName = $"Im{index + 1}";
            var pageWidthText = ToPdfNumber(ToPdfPoints(pdfImage.Width, pdfImage.DpiX));
            var pageHeightText = ToPdfNumber(ToPdfPoints(pdfImage.Height, pdfImage.DpiY));

            WriteObject(
                stream,
                offsets,
                pageObjectId,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageWidthText} {pageHeightText}] /Resources << /XObject << /{imageName} {imageObjectId} 0 R >> >> /Contents {contentObjectId} 0 R >>\n");
            var content = Encoding.ASCII.GetBytes($"q\n{pageWidthText} 0 0 {pageHeightText} 0 0 cm\n/{imageName} Do\nQ\n");
            WriteStreamObject(stream, offsets, contentObjectId, string.Empty, content);
            var imageHeader =
                $"<< /Type /XObject /Subtype /Image /Width {pdfImage.Width} /Height {pdfImage.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {pdfImage.JpegBytes.Length} >>";
            WriteStreamObject(stream, offsets, imageObjectId, imageHeader, pdfImage.JpegBytes);
        }

        var xrefOffset = stream.Position;
        WriteAscii(stream, $"xref\n0 {objectCount + 1}\n");
        WriteAscii(stream, "0000000000 65535 f \n");
        for (var objectId = 1; objectId <= objectCount; objectId++)
        {
            WriteAscii(stream, $"{offsets[objectId].ToString("D10", CultureInfo.InvariantCulture)} 00000 n \n");
        }

        WriteAscii(
            stream,
            $"trailer\n<< /Size {objectCount + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n");
    }

    private PdfImage LoadPdfImage(string path)
    {
        using var loaded = _imageService.LoadBitmapCopy(path);
        using var flattened = new Bitmap(loaded.Width, loaded.Height, PixelFormat.Format24bppRgb);
        ImageProcessingService.TrySetResolution(flattened, loaded);
        using (var graphics = Graphics.FromImage(flattened))
        {
            graphics.Clear(Color.White);
            graphics.DrawImage(loaded, 0, 0, loaded.Width, loaded.Height);
        }

        using var memoryStream = new MemoryStream();
        var codec = ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(encoder => encoder.FormatID == ImageFormat.Jpeg.Guid);
        if (codec is null)
        {
            flattened.Save(memoryStream, ImageFormat.Jpeg);
        }
        else
        {
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 92L);
            flattened.Save(memoryStream, codec, parameters);
        }

        return new PdfImage(
            flattened.Width,
            flattened.Height,
            flattened.HorizontalResolution,
            flattened.VerticalResolution,
            memoryStream.ToArray());
    }

    private static bool IsPdfSourceImage(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff";
    }

    private static void WriteObject(Stream stream, long[] offsets, int objectId, string body)
    {
        offsets[objectId] = stream.Position;
        WriteAscii(stream, $"{objectId} 0 obj\n{body}endobj\n");
    }

    private static void WriteStreamObject(Stream stream, long[] offsets, int objectId, string dictionary, byte[] data)
    {
        offsets[objectId] = stream.Position;
        if (string.IsNullOrWhiteSpace(dictionary))
        {
            dictionary = $"<< /Length {data.Length} >>";
        }

        WriteAscii(stream, $"{objectId} 0 obj\n{dictionary}\nstream\n");
        stream.Write(data, 0, data.Length);
        WriteAscii(stream, "\nendstream\nendobj\n");
    }

    private static void WriteAscii(Stream stream, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static double ToPdfPoints(int pixels, float dpi)
    {
        var safeDpi = dpi is >= 30F and <= 1200F ? dpi : 96F;
        return Math.Clamp(pixels * 72D / safeDpi, 36D, 14400D);
    }

    private static string ToPdfNumber(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private sealed record PdfImage(int Width, int Height, float DpiX, float DpiY, byte[] JpegBytes);

    private sealed class ImageFileNameComparer : IComparer<string>
    {
        public static readonly ImageFileNameComparer Instance = new();

        public int Compare(string? left, string? right)
        {
            if (left is null || right is null)
            {
                return left is null ? (right is null ? 0 : -1) : 1;
            }

            var leftName = Path.GetFileNameWithoutExtension(left);
            var rightName = Path.GetFileNameWithoutExtension(right);
            var leftIsNumber = long.TryParse(leftName, NumberStyles.Integer, CultureInfo.InvariantCulture, out var leftNumber);
            var rightIsNumber = long.TryParse(rightName, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rightNumber);

            if (leftIsNumber && rightIsNumber)
            {
                var comparison = leftNumber.CompareTo(rightNumber);
                if (comparison != 0)
                {
                    return comparison;
                }
            }
            else if (leftIsNumber != rightIsNumber)
            {
                return leftIsNumber ? -1 : 1;
            }
            else
            {
                var comparison = string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return string.Compare(Path.GetFileName(left), Path.GetFileName(right), StringComparison.OrdinalIgnoreCase);
        }
    }
}
