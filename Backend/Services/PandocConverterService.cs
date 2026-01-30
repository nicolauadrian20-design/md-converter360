using System.Text;
using CliWrap;
using CliWrap.Buffered;

namespace MDConverter360.Services;

/// <summary>
/// High-quality document conversion using Pandoc (gold standard for document conversion)
/// Pandoc handles complex formatting much better than pure C# solutions
/// </summary>
public class PandocConverterService : IPandocConverterService
{
    private readonly ILogger<PandocConverterService> _logger;
    private readonly string? _pandocPath;
    private readonly string _tempDirectory;
    private readonly string _referenceDocxPath;

    public PandocConverterService(ILogger<PandocConverterService> logger)
    {
        _logger = logger;
        _pandocPath = FindPandocPath();
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MDConverter360");
        _referenceDocxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "reference.docx");

        if (!Directory.Exists(_tempDirectory))
            Directory.CreateDirectory(_tempDirectory);

        if (_pandocPath != null)
            _logger.LogInformation("Pandoc found at: {Path}", _pandocPath);
        else
            _logger.LogWarning("Pandoc not found - falling back to basic converter");
    }

    public bool IsPandocAvailable => _pandocPath != null;

    public async Task<ConversionResult> ConvertDocxToMarkdownAsync(byte[] docxBytes, string fileName)
    {
        if (!IsPandocAvailable)
            return new ConversionResult { Success = false, ErrorMessage = "Pandoc not available" };

        var inputPath = Path.Combine(_tempDirectory, $"{Guid.NewGuid()}.docx");
        var outputPath = Path.Combine(_tempDirectory, $"{Guid.NewGuid()}.md");

        try
        {
            await File.WriteAllBytesAsync(inputPath, docxBytes);

            // Use extended markdown with pipe tables, raw html for complex formatting
            // --wrap=none preserves original line breaks
            // --extract-media extracts images (optional)
            var args = new List<string>
            {
                inputPath,
                "-f", "docx",
                "-t", "markdown+pipe_tables+raw_html+fenced_divs+bracketed_spans",
                "--wrap=none",
                "--standalone",
                "-o", outputPath
            };

            var result = await Cli.Wrap(_pandocPath!)
                .WithArguments(args)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            if (result.ExitCode != 0)
            {
                _logger.LogError("Pandoc conversion failed: {Error}", result.StandardError);
                return new ConversionResult
                {
                    Success = false,
                    ErrorMessage = $"Pandoc error: {result.StandardError}"
                };
            }

            var markdownContent = await File.ReadAllTextAsync(outputPath);

            // Post-process: clean up pandoc-specific artifacts if needed
            markdownContent = CleanupPandocMarkdown(markdownContent);

            var outputFileName = Path.GetFileNameWithoutExtension(fileName) + ".md";

            return new ConversionResult
            {
                Success = true,
                OutputData = Encoding.UTF8.GetBytes(markdownContent),
                OutputFileName = outputFileName,
                OutputMimeType = "text/markdown",
                Metadata = new ConversionMetadata
                {
                    SourceFormat = "DOCX",
                    TargetFormat = "Markdown",
                    WordCount = CountWords(markdownContent),
                    CharacterCount = markdownContent.Length
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Pandoc DOCX to Markdown conversion");
            return new ConversionResult { Success = false, ErrorMessage = ex.Message };
        }
        finally
        {
            CleanupTempFiles(inputPath, outputPath);
        }
    }

    public async Task<ConversionResult> ConvertMarkdownToDocxAsync(byte[] mdBytes, string fileName)
    {
        if (!IsPandocAvailable)
            return new ConversionResult { Success = false, ErrorMessage = "Pandoc not available" };

        var inputPath = Path.Combine(_tempDirectory, $"{Guid.NewGuid()}.md");
        var outputPath = Path.Combine(_tempDirectory, $"{Guid.NewGuid()}.docx");

        try
        {
            await File.WriteAllBytesAsync(inputPath, mdBytes);

            var args = new List<string>
            {
                inputPath,
                "-f", "markdown+pipe_tables+raw_html+fenced_divs+bracketed_spans+smart",
                "-t", "docx",
                "--standalone"
            };

            // Use reference document if available for consistent styling
            if (File.Exists(_referenceDocxPath))
            {
                args.Add("--reference-doc");
                args.Add(_referenceDocxPath);
                _logger.LogDebug("Using reference document: {Path}", _referenceDocxPath);
            }

            args.Add("-o");
            args.Add(outputPath);

            var result = await Cli.Wrap(_pandocPath!)
                .WithArguments(args)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            if (result.ExitCode != 0)
            {
                _logger.LogError("Pandoc conversion failed: {Error}", result.StandardError);
                return new ConversionResult
                {
                    Success = false,
                    ErrorMessage = $"Pandoc error: {result.StandardError}"
                };
            }

            var docxContent = await File.ReadAllBytesAsync(outputPath);
            var outputFileName = Path.GetFileNameWithoutExtension(fileName) + ".docx";

            return new ConversionResult
            {
                Success = true,
                OutputData = docxContent,
                OutputFileName = outputFileName,
                OutputMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                Metadata = new ConversionMetadata
                {
                    SourceFormat = "Markdown",
                    TargetFormat = "DOCX"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Pandoc Markdown to DOCX conversion");
            return new ConversionResult { Success = false, ErrorMessage = ex.Message };
        }
        finally
        {
            CleanupTempFiles(inputPath, outputPath);
        }
    }

    public async Task<ConversionResult> ConvertPdfToMarkdownAsync(byte[] pdfBytes, string fileName)
    {
        if (!IsPandocAvailable)
            return new ConversionResult { Success = false, ErrorMessage = "Pandoc not available" };

        var inputPath = Path.Combine(_tempDirectory, $"{Guid.NewGuid()}.pdf");
        var outputPath = Path.Combine(_tempDirectory, $"{Guid.NewGuid()}.md");

        try
        {
            await File.WriteAllBytesAsync(inputPath, pdfBytes);

            // Note: Pandoc PDF support requires pdftotext or similar
            // For now, we'll try direct conversion
            var args = new List<string>
            {
                inputPath,
                "-f", "pdf",
                "-t", "markdown+pipe_tables",
                "--wrap=none",
                "-o", outputPath
            };

            var result = await Cli.Wrap(_pandocPath!)
                .WithArguments(args)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            if (result.ExitCode != 0)
            {
                // PDF conversion may not be supported - this is expected
                _logger.LogWarning("Pandoc PDF conversion failed (expected): {Error}", result.StandardError);
                return new ConversionResult
                {
                    Success = false,
                    ErrorMessage = "PDF conversion requires additional tools. Use fallback converter."
                };
            }

            var markdownContent = await File.ReadAllTextAsync(outputPath);
            var outputFileName = Path.GetFileNameWithoutExtension(fileName) + ".md";

            return new ConversionResult
            {
                Success = true,
                OutputData = Encoding.UTF8.GetBytes(markdownContent),
                OutputFileName = outputFileName,
                OutputMimeType = "text/markdown",
                Metadata = new ConversionMetadata
                {
                    SourceFormat = "PDF",
                    TargetFormat = "Markdown",
                    WordCount = CountWords(markdownContent),
                    CharacterCount = markdownContent.Length
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Pandoc PDF to Markdown conversion");
            return new ConversionResult { Success = false, ErrorMessage = ex.Message };
        }
        finally
        {
            CleanupTempFiles(inputPath, outputPath);
        }
    }

    public async Task<ConversionResult> ConvertMarkdownToPdfAsync(byte[] mdBytes, string fileName)
    {
        if (!IsPandocAvailable)
            return new ConversionResult { Success = false, ErrorMessage = "Pandoc not available" };

        var inputPath = Path.Combine(_tempDirectory, $"{Guid.NewGuid()}.md");
        var outputPath = Path.Combine(_tempDirectory, $"{Guid.NewGuid()}.pdf");

        try
        {
            await File.WriteAllBytesAsync(inputPath, mdBytes);

            // PDF generation requires a PDF engine (wkhtmltopdf, weasyprint, or LaTeX)
            var args = new List<string>
            {
                inputPath,
                "-f", "markdown+pipe_tables+smart",
                "-t", "pdf",
                "--pdf-engine=wkhtmltopdf", // or weasyprint
                "-V", "geometry:margin=1in",
                "-o", outputPath
            };

            var result = await Cli.Wrap(_pandocPath!)
                .WithArguments(args)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            if (result.ExitCode != 0)
            {
                _logger.LogWarning("Pandoc PDF generation failed: {Error}", result.StandardError);
                return new ConversionResult
                {
                    Success = false,
                    ErrorMessage = "PDF generation requires wkhtmltopdf or LaTeX. Use fallback converter."
                };
            }

            var pdfContent = await File.ReadAllBytesAsync(outputPath);
            var outputFileName = Path.GetFileNameWithoutExtension(fileName) + ".pdf";

            return new ConversionResult
            {
                Success = true,
                OutputData = pdfContent,
                OutputFileName = outputFileName,
                OutputMimeType = "application/pdf",
                Metadata = new ConversionMetadata
                {
                    SourceFormat = "Markdown",
                    TargetFormat = "PDF"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Pandoc Markdown to PDF conversion");
            return new ConversionResult { Success = false, ErrorMessage = ex.Message };
        }
        finally
        {
            CleanupTempFiles(inputPath, outputPath);
        }
    }

    private string CleanupPandocMarkdown(string markdown)
    {
        // Remove pandoc title block if present
        if (markdown.StartsWith("---"))
        {
            var endIndex = markdown.IndexOf("---", 3);
            if (endIndex > 0)
            {
                var afterYaml = markdown.Substring(endIndex + 3).TrimStart('\r', '\n');
                if (!string.IsNullOrWhiteSpace(afterYaml))
                    markdown = afterYaml;
            }
        }

        // Clean up excessive blank lines
        markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"\n{4,}", "\n\n\n");

        return markdown.Trim();
    }

    private static string? FindPandocPath()
    {
        // Check common installation paths
        var paths = new[]
        {
            @"C:\Program Files\Pandoc\pandoc.exe",
            @"C:\Program Files (x86)\Pandoc\pandoc.exe",
            Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Pandoc\pandoc.exe"),
            "pandoc" // Will be found via PATH
        };

        foreach (var path in paths)
        {
            if (path == "pandoc")
            {
                // Check if pandoc is in PATH
                try
                {
                    var result = Cli.Wrap("where")
                        .WithArguments("pandoc")
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync()
                        .GetAwaiter()
                        .GetResult();

                    if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput))
                        return result.StandardOutput.Trim().Split('\n')[0].Trim();
                }
                catch { }
            }
            else if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private void CleanupTempFiles(params string[] paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temp file: {Path}", path);
            }
        }
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}

public interface IPandocConverterService
{
    bool IsPandocAvailable { get; }
    Task<ConversionResult> ConvertDocxToMarkdownAsync(byte[] docxBytes, string fileName);
    Task<ConversionResult> ConvertMarkdownToDocxAsync(byte[] mdBytes, string fileName);
    Task<ConversionResult> ConvertPdfToMarkdownAsync(byte[] pdfBytes, string fileName);
    Task<ConversionResult> ConvertMarkdownToPdfAsync(byte[] mdBytes, string fileName);
}
