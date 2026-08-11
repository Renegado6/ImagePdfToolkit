using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using RandomWatermarkTool.Models;
using DrawingImage = System.Drawing.Image;
using DrawingSize = System.Drawing.Size;
using ImageSource = System.Windows.Media.ImageSource;

namespace RandomWatermarkTool.Services;

public sealed class ImageProcessingService
{
    public Bitmap LoadBitmapCopy(string path)
    {
        using var stream = File.OpenRead(path);
        using var image = DrawingImage.FromStream(stream);
        return new Bitmap(image);
    }

    public ImageSource CreatePreviewSource(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public WatermarkRenderResult Render(
        Bitmap source,
        Bitmap watermarkSource,
        WatermarkRenderState state,
        int sizePercent,
        int colorDepthPercent,
        int offsetXPercent,
        int offsetYPercent)
    {
        using var watermark = CreateRotatedWatermark(
            watermarkSource,
            source.Size,
            state.Angle,
            Math.Clamp(sizePercent / 100F, 0.1F, 3F));
        var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        TrySetResolution(result, source);
        int x;
        int y;

        using (var graphics = Graphics.FromImage(result))
        {
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(source, 0, 0, source.Width, source.Height);

            var maxX = Math.Max(0, result.Width - watermark.Width);
            var maxY = Math.Max(0, result.Height - watermark.Height);
            x = GetWatermarkPosition(maxX, state.AnchorX, offsetXPercent);
            y = GetWatermarkPosition(maxY, state.AnchorY, offsetYPercent);
        }

        var effectiveOpacity = Math.Clamp(state.Opacity * colorDepthPercent / 100F, 0F, 1F);
        ApplyScannedStampWatermark(result, watermark, x, y, effectiveOpacity, state.Seed);
        return new WatermarkRenderResult(result, effectiveOpacity);
    }

    public void SaveByExtension(Bitmap image, string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".png")
        {
            image.Save(path, ImageFormat.Png);
            return;
        }

        var imageFormat = extension switch
        {
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".bmp" => ImageFormat.Bmp,
            ".gif" => ImageFormat.Gif,
            ".tif" or ".tiff" => ImageFormat.Tiff,
            _ => ImageFormat.Png
        };

        if (imageFormat.Guid == ImageFormat.Png.Guid)
        {
            image.Save(path, ImageFormat.Png);
            return;
        }

        using var flattened = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb);
        TrySetResolution(flattened, image);
        using (var graphics = Graphics.FromImage(flattened))
        {
            graphics.Clear(Color.White);
            graphics.DrawImage(image, 0, 0, image.Width, image.Height);
        }

        if (imageFormat.Guid == ImageFormat.Jpeg.Guid)
        {
            SaveJpeg(flattened, path, 94L);
        }
        else
        {
            flattened.Save(path, imageFormat);
        }
    }

    public static bool IsSupportedSourceImage(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff";
    }

    public static bool IsPngFile(string path)
        => string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase);

    public static void TrySetResolution(Bitmap target, DrawingImage source)
    {
        try
        {
            if (source.HorizontalResolution > 1F && source.VerticalResolution > 1F)
            {
                target.SetResolution(source.HorizontalResolution, source.VerticalResolution);
            }
        }
        catch
        {
            // Resolution metadata is best-effort; image pixels remain valid if it cannot be copied.
        }
    }

    public static void SaveJpeg(Bitmap image, string path, long quality)
    {
        var codec = ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(encoder => encoder.FormatID == ImageFormat.Jpeg.Guid);
        if (codec is null)
        {
            image.Save(path, ImageFormat.Jpeg);
            return;
        }

        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
        image.Save(path, codec, parameters);
    }

    private static int GetWatermarkPosition(int maxCoordinate, double anchorRatio, int offsetPercent)
    {
        if (maxCoordinate <= 0)
        {
            return 0;
        }

        var baseCoordinate = (int)Math.Round(maxCoordinate * Math.Clamp(anchorRatio, 0D, 1D));
        var offset = (int)Math.Round(maxCoordinate * offsetPercent / 100D);
        return Math.Clamp(baseCoordinate + offset, 0, maxCoordinate);
    }

    private static Bitmap CreateRotatedWatermark(
        Bitmap source,
        DrawingSize canvasSize,
        float angle,
        float sizeMultiplier)
    {
        var scale = GetWatermarkScale(source.Size, canvasSize, angle, sizeMultiplier);
        var scaledWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
        var scaledHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
        var rotatedSize = GetRotatedSize(scaledWidth, scaledHeight, angle);

        var result = new Bitmap(rotatedSize.Width, rotatedSize.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.Clear(Color.Transparent);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.TranslateTransform(rotatedSize.Width / 2F, rotatedSize.Height / 2F);
        graphics.RotateTransform(angle);
        graphics.DrawImage(
            source,
            new Rectangle(-scaledWidth / 2, -scaledHeight / 2, scaledWidth, scaledHeight),
            0,
            0,
            source.Width,
            source.Height,
            GraphicsUnit.Pixel);
        return result;
    }

    private static void ApplyScannedStampWatermark(
        Bitmap target,
        Bitmap watermark,
        int left,
        int top,
        float opacity,
        int seed)
    {
        var targetBounds = new Rectangle(0, 0, target.Width, target.Height);
        var placedBounds = new Rectangle(left, top, watermark.Width, watermark.Height);
        var blendBounds = Rectangle.Intersect(targetBounds, placedBounds);
        if (blendBounds.Width <= 0 || blendBounds.Height <= 0)
        {
            return;
        }

        var watermarkBounds = new Rectangle(
            blendBounds.X - left,
            blendBounds.Y - top,
            blendBounds.Width,
            blendBounds.Height);
        var targetData = target.LockBits(blendBounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        var watermarkData = watermark.LockBits(watermarkBounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            var targetStride = Math.Abs(targetData.Stride);
            var watermarkStride = Math.Abs(watermarkData.Stride);
            var targetBytes = targetStride * blendBounds.Height;
            var watermarkBytes = watermarkStride * blendBounds.Height;
            var targetPixels = new byte[targetBytes];
            var watermarkPixels = new byte[watermarkBytes];
            Marshal.Copy(targetData.Scan0, targetPixels, 0, targetBytes);
            Marshal.Copy(watermarkData.Scan0, watermarkPixels, 0, watermarkBytes);

            for (var row = 0; row < blendBounds.Height; row++)
            {
                var targetRow = row * targetStride;
                var watermarkRow = row * watermarkStride;
                for (var column = 0; column < blendBounds.Width; column++)
                {
                    var targetIndex = targetRow + column * 4;
                    var watermarkIndex = watermarkRow + column * 4;
                    var watermarkAlpha = watermarkPixels[watermarkIndex + 3];
                    if (watermarkAlpha == 0)
                    {
                        continue;
                    }

                    var paperLuminance =
                        (targetPixels[targetIndex + 2] * 0.299F
                         + targetPixels[targetIndex + 1] * 0.587F
                         + targetPixels[targetIndex] * 0.114F) / 255F;
                    var largeGrain = 0.86F + ValueNoise(seed, left + column, top + row, 0.045F) * 0.18F;
                    var contactPatch = ValueNoise(seed + 43, left + column, top + row, 0.075F);
                    var contactInk = contactPatch switch
                    {
                        < 0.045F => 0.52F,
                        < 0.10F => 0.78F,
                        _ => 1F
                    };
                    var fineGrain = HashNoise(seed, left + column, top + row);
                    var smallSpot = ValueNoise(seed + 83, left + column, top + row, 0.22F);
                    var dryInk = fineGrain switch
                    {
                        < 0.018F => 0.05F,
                        < 0.055F => 0.32F,
                        _ when smallSpot < 0.07F && HashNoise(seed + 151, left + column, top + row) < 0.42F => 0.45F,
                        < 0.12F => 0.68F,
                        _ => 1F
                    };
                    var alpha = watermarkAlpha / 255F;
                    var softenedEdge = 0.72F + Math.Min(1F, alpha * 1.4F) * 0.28F;
                    var edgeWear = alpha < 0.42F && HashNoise(seed + 199, left + column, top + row) < 0.32F
                        ? 0.38F
                        : 1F;
                    var blendAmount = Math.Clamp(
                        opacity
                        * alpha
                        * largeGrain
                        * contactInk
                        * dryInk
                        * (0.66F + paperLuminance * 0.34F)
                        * softenedEdge
                        * edgeWear,
                        0F,
                        1F);
                    if (blendAmount <= 0.002F)
                    {
                        continue;
                    }

                    BlendMultiplyChannel(targetPixels, targetIndex, watermarkPixels[watermarkIndex], blendAmount);
                    BlendMultiplyChannel(targetPixels, targetIndex + 1, watermarkPixels[watermarkIndex + 1], blendAmount);
                    BlendMultiplyChannel(targetPixels, targetIndex + 2, watermarkPixels[watermarkIndex + 2], blendAmount);
                }
            }

            Marshal.Copy(targetPixels, 0, targetData.Scan0, targetBytes);
        }
        finally
        {
            watermark.UnlockBits(watermarkData);
            target.UnlockBits(targetData);
        }
    }

    private static void BlendMultiplyChannel(byte[] pixels, int index, byte watermarkValue, float blendAmount)
    {
        var backgroundValue = pixels[index];
        var multiplied = backgroundValue * watermarkValue / 255F;
        pixels[index] = (byte)Math.Clamp(
            (int)Math.Round(backgroundValue + (multiplied - backgroundValue) * blendAmount),
            0,
            255);
    }

    private static float ValueNoise(int seed, int x, int y, float scale)
    {
        var sampleX = x * scale;
        var sampleY = y * scale;
        var x0 = (int)Math.Floor(sampleX);
        var y0 = (int)Math.Floor(sampleY);
        var tx = sampleX - x0;
        var ty = sampleY - y0;
        var sx = SmoothStep(tx);
        var sy = SmoothStep(ty);
        return Lerp(
            Lerp(HashNoise(seed, x0, y0), HashNoise(seed, x0 + 1, y0), sx),
            Lerp(HashNoise(seed, x0, y0 + 1), HashNoise(seed, x0 + 1, y0 + 1), sx),
            sy);
    }

    private static float HashNoise(int seed, int x, int y)
    {
        unchecked
        {
            var hash = seed;
            hash ^= x * 374761393;
            hash = (hash << 13) | (int)((uint)hash >> 19);
            hash ^= y * 668265263;
            hash *= 1274126177;
            hash ^= hash >> 16;
            return ((uint)hash & 0x00FFFFFF) / 16777215F;
        }
    }

    private static float SmoothStep(float value) => value * value * (3F - 2F * value);

    private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;

    private static float GetWatermarkScale(
        DrawingSize watermarkSize,
        DrawingSize canvasSize,
        float angle,
        float sizeMultiplier)
    {
        var maxWidth = Math.Max(1F, canvasSize.Width * 0.45F);
        var maxHeight = Math.Max(1F, canvasSize.Height * 0.45F);
        var baseScale = Math.Min(1F, Math.Min(maxWidth / watermarkSize.Width, maxHeight / watermarkSize.Height));
        var scale = baseScale * Math.Clamp(sizeMultiplier, 0.1F, 3F);
        var rotatedSize = GetRotatedSize(
            Math.Max(1, (int)Math.Round(watermarkSize.Width * scale)),
            Math.Max(1, (int)Math.Round(watermarkSize.Height * scale)),
            angle);

        if (rotatedSize.Width > canvasSize.Width || rotatedSize.Height > canvasSize.Height)
        {
            var fitScale = Math.Min(
                canvasSize.Width / (float)rotatedSize.Width,
                canvasSize.Height / (float)rotatedSize.Height);
            scale *= Math.Max(0.01F, fitScale * 0.98F);
        }

        return scale;
    }

    private static DrawingSize GetRotatedSize(int width, int height, float angle)
    {
        var radians = Math.Abs(angle) * Math.PI / 180D;
        var sin = Math.Abs(Math.Sin(radians));
        var cos = Math.Abs(Math.Cos(radians));
        return new DrawingSize(
            Math.Max(1, (int)Math.Ceiling(width * cos + height * sin)),
            Math.Max(1, (int)Math.Ceiling(width * sin + height * cos)));
    }
}

public sealed record WatermarkRenderResult(Bitmap Image, float EffectiveOpacity);
