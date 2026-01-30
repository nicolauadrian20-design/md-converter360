using Microsoft.AspNetCore.Mvc;
using MDConverter360.Services;

namespace MDConverter360.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConversionController : ControllerBase
{
    private readonly IConverterService _converterService;
    private readonly ILogger<ConversionController> _logger;
    private readonly string _downloadsPath;

    public ConversionController(IConverterService converterService, ILogger<ConversionController> logger)
    {
        _converterService = converterService;
        _logger = logger;
        _downloadsPath = GetDownloadsFolder();
    }

    private static string GetDownloadsFolder()
    {
        // Primary: D:\Downloads (known user location)
        const string primaryPath = @"D:\Downloads";
        if (Directory.Exists(primaryPath))
            return primaryPath;

        // Fallback: Standard Windows Downloads
        var standardPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(standardPath))
            return standardPath;

        // Last resort: Create D:\Downloads
        try
        {
            Directory.CreateDirectory(primaryPath);
            return primaryPath;
        }
        catch
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
    }

    /// <summary>
    /// Convert a single file
    /// </summary>
    [HttpPost("convert")]
    public async Task<IActionResult> ConvertFile(
        IFormFile file,
        [FromQuery] string? targetFormat = null,
        [FromQuery] bool saveToDownloads = false)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided" });
        }

        if (!_converterService.IsSupported(file.FileName))
        {
            return BadRequest(new { error = $"Unsupported file format: {Path.GetExtension(file.FileName)}" });
        }

        try
        {
            var conversionType = _converterService.DetectConversionType(file.FileName, targetFormat);

            using var stream = file.OpenReadStream();
            var result = await _converterService.ConvertAsync(stream, file.FileName, conversionType);

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            // Save to Downloads folder if requested
            if (saveToDownloads && result.OutputData != null && result.OutputFileName != null)
            {
                var downloadPath = GetUniqueFilePath(_downloadsPath, result.OutputFileName);
                await System.IO.File.WriteAllBytesAsync(downloadPath, result.OutputData);
                _logger.LogInformation("File saved to Downloads: {Path}", downloadPath);
            }

            // Return the file
            return File(result.OutputData!, result.OutputMimeType!, result.OutputFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Conversion failed for {FileName}", file.FileName);
            return StatusCode(500, new { error = "Conversion failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Convert multiple files (batch conversion)
    /// </summary>
    [HttpPost("convert-batch")]
    public async Task<IActionResult> ConvertBatch(
        [FromForm] List<IFormFile> files,
        [FromQuery] string? targetFormat = null,
        [FromQuery] bool saveToDownloads = true)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest(new { error = "No files provided" });
        }

        var results = new List<BatchConversionResult>();

        foreach (var file in files)
        {
            var itemResult = new BatchConversionResult
            {
                OriginalFileName = file.FileName
            };

            if (!_converterService.IsSupported(file.FileName))
            {
                itemResult.Success = false;
                itemResult.Error = $"Unsupported format: {Path.GetExtension(file.FileName)}";
                results.Add(itemResult);
                continue;
            }

            try
            {
                var conversionType = _converterService.DetectConversionType(file.FileName, targetFormat);

                using var stream = file.OpenReadStream();
                var result = await _converterService.ConvertAsync(stream, file.FileName, conversionType);

                if (result.Success && result.OutputData != null && result.OutputFileName != null)
                {
                    itemResult.Success = true;
                    itemResult.OutputFileName = result.OutputFileName;
                    itemResult.Metadata = result.Metadata;

                    if (saveToDownloads)
                    {
                        var downloadPath = GetUniqueFilePath(_downloadsPath, result.OutputFileName);
                        await System.IO.File.WriteAllBytesAsync(downloadPath, result.OutputData);
                        itemResult.SavedPath = downloadPath;
                        _logger.LogInformation("Batch file saved: {Path}", downloadPath);
                    }
                }
                else
                {
                    itemResult.Success = false;
                    itemResult.Error = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                itemResult.Success = false;
                itemResult.Error = ex.Message;
                _logger.LogError(ex, "Batch conversion failed for {FileName}", file.FileName);
            }

            results.Add(itemResult);
        }

        return Ok(new
        {
            total = files.Count,
            successful = results.Count(r => r.Success),
            failed = results.Count(r => !r.Success),
            results
        });
    }

    /// <summary>
    /// Get supported formats
    /// </summary>
    [HttpGet("formats")]
    public IActionResult GetSupportedFormats()
    {
        return Ok(new
        {
            inputFormats = new[]
            {
                new { extension = ".pdf", description = "PDF Document", convertsTo = "Markdown (.md)" },
                new { extension = ".docx", description = "Microsoft Word", convertsTo = "Markdown (.md)" },
                new { extension = ".doc", description = "Microsoft Word (Legacy)", convertsTo = "Markdown (.md)" },
                new { extension = ".odt", description = "OpenDocument Text", convertsTo = "Markdown (.md)" },
                new { extension = ".md", description = "Markdown", convertsTo = "PDF (.pdf) or Word (.docx)" },
                new { extension = ".markdown", description = "Markdown", convertsTo = "PDF (.pdf) or Word (.docx)" }
            },
            outputFormats = new[]
            {
                new { extension = ".md", mimeType = "text/markdown" },
                new { extension = ".pdf", mimeType = "application/pdf" },
                new { extension = ".docx", mimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document" }
            }
        });
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            service = "MD.converter360",
            timestamp = DateTime.UtcNow,
            version = "1.0.0",
            downloadsPath = _downloadsPath
        });
    }

    private string GetUniqueFilePath(string directory, string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var filePath = Path.Combine(directory, fileName);

        var counter = 1;
        while (System.IO.File.Exists(filePath))
        {
            filePath = Path.Combine(directory, $"{baseName}_{counter}{extension}");
            counter++;
        }

        return filePath;
    }
}

public class BatchConversionResult
{
    public string OriginalFileName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? OutputFileName { get; set; }
    public string? SavedPath { get; set; }
    public string? Error { get; set; }
    public ConversionMetadata? Metadata { get; set; }
}
