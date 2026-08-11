# RandomWatermarkTool

[English](README.md) | [简体中文](README.zh-CN.md)

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D4)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Latest release](https://img.shields.io/github/v/release/shadiao719/RandomWatermarkTool)](https://github.com/shadiao719/RandomWatermarkTool/releases/latest)

RandomWatermarkTool is a lightweight Windows desktop application for applying randomized PNG watermarks to images. It is built with WPF and MVVM, supports multiple watermark candidates, and can also merge images from an output folder into a PDF.

![WPF interface preview](design/wpf-ui-concept.png)

> This is a design preview of the WPF interface. The actual appearance may vary slightly depending on window size and system fonts.

## Download

Download the latest ready-to-run Windows package from the [Releases page](https://github.com/shadiao719/RandomWatermarkTool/releases/latest).

1. Download `RandomWatermarkTool-v1.0.0-win-x64.zip`.
2. Extract the ZIP file to any folder.
3. Run `RandomWatermarkTool.exe`.

The portable build is self-contained and does not require a separate .NET installation. Windows may show a SmartScreen warning because the executable is not code-signed; review the publisher information and choose to run it only if you downloaded it from this repository.

The application interface in v1.0.0 is currently Simplified Chinese. This English guide includes translations for the main actions.

## Features

- Drag and drop or select a source image: PNG, JPG, BMP, GIF, or TIFF
- Configure up to 10 PNG watermark candidates
- Randomize watermark selection, rotation, opacity, and placement
- Adjust watermark size and color intensity
- Move the watermark with four directional buttons in 5% steps
- Reset the watermark position with the center button
- Preview and save the processed image
- Remember the last source image, watermarks, output folder, and settings
- Merge images from the output folder into a PDF
- Process all images locally without uploading them to a server

## Requirements for Development

- Windows 10 or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2026 or another .NET-compatible development environment (optional)

## Build from Source

Clone the repository:

```powershell
git clone https://github.com/shadiao719/RandomWatermarkTool.git
cd RandomWatermarkTool
```

Build and run:

```powershell
dotnet build
dotnet run --project RandomWatermarkTool.csproj
```

You can also open `RandomWatermarkTool.slnx` in Visual Studio and run the project directly.

## Usage

1. Drop a source image into the preview area or choose one from disk.
2. Add one or more PNG watermarks on the right.
3. Adjust the size, intensity, and position.
4. Select **随机盖水印** (Apply Random Watermark) to preview a result.
5. Choose an output folder different from the source folder, then save the result.
6. To merge output images, select **目录图片生成 PDF** (Create PDF from Folder Images).

## Project Structure

```text
Controls/        Custom WPF controls
Infrastructure/ MVVM infrastructure
Models/          Data models and constants
Services/        Image, PDF, settings, and dialog services
ViewModels/      Main window view model
design/          Interface design preview
```

## Contributing

Issues and pull requests are welcome. Before submitting code, make sure the Release build succeeds:

```powershell
dotnet build -c Release
```

## License

This project is available under the [MIT License](LICENSE).
