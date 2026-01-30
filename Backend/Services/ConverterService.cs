using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace MDConverter360.Services;

public class ConverterService : IConverterService
{
    private readonly ILogger<ConverterService> _logger;
    private readonly MarkdownPipeline _markdownPipeline;
    private readonly IPandocConverterService? _pandocConverter;

    private static readonly Dictionary<string, string> MimeTypes = new()
    {
        { ".md", "text/markdown" },
        { ".pdf", "application/pdf" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".odt", "application/vnd.oasis.opendocument.text" }
    };

    public ConverterService(ILogger<ConverterService> logger, IPandocConverterService? pandocConverter = null)
    {
        _logger = logger;
        _pandocConverter = pandocConverter;
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UsePipeTables()
            .UseAutoLinks()
            .UseTaskLists()
            .UseEmphasisExtras()
            .Build();

        if (_pandocConverter?.IsPandocAvailable == true)
            _logger.LogInformation("Pandoc converter available - using high-quality conversion");
        else
            _logger.LogInformation("Pandoc not available - using basic C# converter");
    }

    public async Task<ConversionResult> ConvertAsync(Stream inputStream, string inputFileName, ConversionType conversionType)
    {
        using var memoryStream = new MemoryStream();
        await inputStream.CopyToAsync(memoryStream);
        return await ConvertAsync(memoryStream.ToArray(), inputFileName, conversionType);
    }

    public async Task<ConversionResult> ConvertAsync(byte[] inputBytes, string inputFileName, ConversionType conversionType)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting conversion: {FileName} -> {ConversionType}", inputFileName, conversionType);

            var result = conversionType switch
            {
                ConversionType.PdfToMarkdown => await ConvertPdfToMarkdownAsync(inputBytes, inputFileName),
                ConversionType.DocxToMarkdown => await ConvertDocxToMarkdownAsync(inputBytes, inputFileName),
                ConversionType.OdtToMarkdown => await ConvertOdtToMarkdownAsync(inputBytes, inputFileName),
                ConversionType.MarkdownToPdf => await ConvertMarkdownToPdfAsync(inputBytes, inputFileName),
                ConversionType.MarkdownToDocx => await ConvertMarkdownToDocxAsync(inputBytes, inputFileName),
                _ => throw new ArgumentException($"Unsupported conversion type: {conversionType}")
            };

            stopwatch.Stop();

            if (result.Metadata != null)
            {
                result.Metadata.ProcessingTime = stopwatch.Elapsed;
            }

            _logger.LogInformation("Conversion completed in {ElapsedMs}ms: {FileName}",
                stopwatch.ElapsedMilliseconds, result.OutputFileName);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Conversion failed for {FileName}", inputFileName);
            return new ConversionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public ConversionType DetectConversionType(string inputFileName, string? targetFormat = null)
    {
        var ext = Path.GetExtension(inputFileName).ToLowerInvariant();

        return (ext, targetFormat?.ToLowerInvariant()) switch
        {
            (".pdf", _) => ConversionType.PdfToMarkdown,
            (".docx", _) => ConversionType.DocxToMarkdown,
            (".doc", _) => ConversionType.DocxToMarkdown,
            (".odt", _) => ConversionType.OdtToMarkdown,
            (".md", "pdf") => ConversionType.MarkdownToPdf,
            (".md", "docx") => ConversionType.MarkdownToDocx,
            (".md", _) => ConversionType.MarkdownToPdf,
            (".markdown", "pdf") => ConversionType.MarkdownToPdf,
            (".markdown", "docx") => ConversionType.MarkdownToDocx,
            (".markdown", _) => ConversionType.MarkdownToPdf,
            _ => throw new ArgumentException($"Unsupported file format: {ext}")
        };
    }

    public bool IsSupported(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".pdf" or ".docx" or ".doc" or ".odt" or ".md" or ".markdown";
    }

    #region PDF to Markdown

    private Task<ConversionResult> ConvertPdfToMarkdownAsync(byte[] pdfBytes, string fileName)
    {
        var markdown = new StringBuilder();
        var metadata = new ConversionMetadata { SourceFormat = "PDF", TargetFormat = "Markdown" };

        using var document = PdfDocument.Open(pdfBytes);
        metadata.PageCount = document.NumberOfPages;

        foreach (var page in document.GetPages())
        {
            var pageText = ExtractStructuredTextFromPdfPage(page);
            markdown.Append(pageText);
        }

        var markdownText = CleanupMarkdown(markdown.ToString());
        metadata.WordCount = CountWords(markdownText);
        metadata.CharacterCount = markdownText.Length;

        var outputFileName = Path.GetFileNameWithoutExtension(fileName) + ".md";

        return Task.FromResult(new ConversionResult
        {
            Success = true,
            OutputData = Encoding.UTF8.GetBytes(markdownText),
            OutputFileName = outputFileName,
            OutputMimeType = MimeTypes[".md"],
            Metadata = metadata
        });
    }

    private string ExtractStructuredTextFromPdfPage(Page page)
    {
        var result = new StringBuilder();
        var words = page.GetWords().ToList();

        if (words.Count == 0)
        {
            return page.Text + "\n\n";
        }

        // Group words by lines based on Y position
        var lines = new List<List<Word>>();
        var currentLine = new List<Word>();
        double? lastY = null;

        foreach (var word in words.OrderBy(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left))
        {
            if (lastY == null || Math.Abs(word.BoundingBox.Bottom - lastY.Value) < 5)
            {
                currentLine.Add(word);
            }
            else
            {
                if (currentLine.Count > 0)
                {
                    lines.Add(currentLine.OrderBy(w => w.BoundingBox.Left).ToList());
                }
                currentLine = new List<Word> { word };
            }
            lastY = word.BoundingBox.Bottom;
        }

        if (currentLine.Count > 0)
        {
            lines.Add(currentLine.OrderBy(w => w.BoundingBox.Left).ToList());
        }

        // Process lines
        foreach (var line in lines)
        {
            var lineText = string.Join(" ", line.Select(w => w.Text)).Trim();
            if (string.IsNullOrWhiteSpace(lineText)) continue;

            // Detect headers based on font size (if available) or patterns
            var avgFontSize = line.Average(w => w.Letters.FirstOrDefault()?.PointSize ?? 12);

            if (avgFontSize > 16 || IsLikelyHeader(lineText))
            {
                if (avgFontSize > 20)
                    result.AppendLine($"# {lineText}");
                else if (avgFontSize > 16)
                    result.AppendLine($"## {lineText}");
                else
                    result.AppendLine($"### {lineText}");
                result.AppendLine();
            }
            else if (lineText.StartsWith("•") || lineText.StartsWith("-") ||
                     lineText.StartsWith("*") || Regex.IsMatch(lineText, @"^[a-z]\)") ||
                     Regex.IsMatch(lineText, @"^\d+[\.\)]\s"))
            {
                // List item
                var cleanText = Regex.Replace(lineText, @"^[•\-\*a-z\d][\.\)]*\s*", "").Trim();
                result.AppendLine($"- {cleanText}");
            }
            else
            {
                result.AppendLine(lineText);
            }
        }

        result.AppendLine();
        return result.ToString();
    }

    private bool IsLikelyHeader(string line)
    {
        if (line.Length < 3 || line.Length > 150) return false;

        // All caps
        if (line == line.ToUpperInvariant() && line.Any(char.IsLetter) && line.Length < 100)
            return true;

        // Romanian/English section headers
        if (Regex.IsMatch(line, @"^(CAPITOLUL|SECȚIUNEA|ARTICOLUL|SECTIUNEA|CHAPTER|SECTION|ARTICLE)\s", RegexOptions.IgnoreCase))
            return true;

        // Numbered sections
        if (Regex.IsMatch(line, @"^\d+\.\d*\s+[A-Z]") && line.Length < 80)
            return true;

        return false;
    }

    private string CleanupMarkdown(string markdown)
    {
        // Remove excessive blank lines
        markdown = Regex.Replace(markdown, @"\n{4,}", "\n\n\n");
        // Clean up spacing around headers
        markdown = Regex.Replace(markdown, @"(#{1,6} .+)\n{3,}", "$1\n\n");
        return markdown.Trim();
    }

    #endregion

    #region DOCX to Markdown

    private async Task<ConversionResult> ConvertDocxToMarkdownAsync(byte[] docxBytes, string fileName)
    {
        // Try Pandoc first for high-quality conversion
        if (_pandocConverter?.IsPandocAvailable == true)
        {
            _logger.LogDebug("Using Pandoc for DOCX to Markdown conversion");
            var pandocResult = await _pandocConverter.ConvertDocxToMarkdownAsync(docxBytes, fileName);
            if (pandocResult.Success)
                return pandocResult;

            _logger.LogWarning("Pandoc conversion failed, falling back to C# converter: {Error}", pandocResult.ErrorMessage);
        }

        // Fallback to C# implementation
        _logger.LogDebug("Using C# OpenXML for DOCX to Markdown conversion");
        var markdown = new StringBuilder();
        var metadata = new ConversionMetadata { SourceFormat = "DOCX", TargetFormat = "Markdown" };

        using var memoryStream = new MemoryStream(docxBytes);
        using var document = WordprocessingDocument.Open(memoryStream, false);

        var body = document.MainDocumentPart?.Document.Body;
        if (body == null)
        {
            return new ConversionResult
            {
                Success = false,
                ErrorMessage = "Invalid DOCX document: no body found"
            };
        }

        var numberingPart = document.MainDocumentPart?.NumberingDefinitionsPart;
        var stylesPart = document.MainDocumentPart?.StyleDefinitionsPart;

        foreach (var element in body.Elements())
        {
            ProcessDocxElement(element, markdown, numberingPart, stylesPart);
        }

        var markdownText = CleanupMarkdown(markdown.ToString());
        metadata.WordCount = CountWords(markdownText);
        metadata.CharacterCount = markdownText.Length;
        metadata.PageCount = 1;

        var outputFileName = Path.GetFileNameWithoutExtension(fileName) + ".md";

        return new ConversionResult
        {
            Success = true,
            OutputData = Encoding.UTF8.GetBytes(markdownText),
            OutputFileName = outputFileName,
            OutputMimeType = MimeTypes[".md"],
            Metadata = metadata
        };
    }

    private void ProcessDocxElement(OpenXmlElement element, StringBuilder markdown,
        NumberingDefinitionsPart? numberingPart, StyleDefinitionsPart? stylesPart)
    {
        switch (element)
        {
            case Paragraph para:
                ProcessDocxParagraph(para, markdown, numberingPart, stylesPart);
                break;
            case Table table:
                ProcessDocxTable(table, markdown);
                break;
        }
    }

    private void ProcessDocxParagraph(Paragraph para, StringBuilder markdown,
        NumberingDefinitionsPart? numberingPart, StyleDefinitionsPart? stylesPart)
    {
        var text = GetParagraphFormattedText(para);

        if (string.IsNullOrWhiteSpace(text))
        {
            markdown.AppendLine();
            return;
        }

        // Check for heading styles
        var styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        var headingLevel = GetHeadingLevel(styleId, stylesPart);

        if (headingLevel > 0)
        {
            markdown.AppendLine($"{new string('#', headingLevel)} {text.Trim()}");
            markdown.AppendLine();
        }
        else if (para.ParagraphProperties?.NumberingProperties != null)
        {
            // Check if it's a numbered list or bullet
            var numId = para.ParagraphProperties.NumberingProperties.NumberingId?.Val?.Value;
            var ilvl = para.ParagraphProperties.NumberingProperties.NumberingLevelReference?.Val?.Value ?? 0;
            var indent = new string(' ', ilvl * 2);

            markdown.AppendLine($"{indent}- {text.Trim()}");
        }
        else
        {
            markdown.AppendLine(text.Trim());
            markdown.AppendLine();
        }
    }

    private string GetParagraphFormattedText(Paragraph para)
    {
        var result = new StringBuilder();

        foreach (var run in para.Elements<Run>())
        {
            var text = run.InnerText;
            if (string.IsNullOrEmpty(text)) continue;

            var runProps = run.RunProperties;
            var isBold = runProps?.Bold != null || runProps?.Bold?.Val?.Value == true;
            var isItalic = runProps?.Italic != null || runProps?.Italic?.Val?.Value == true;
            var isCode = runProps?.GetFirstChild<RunFonts>()?.Ascii?.Value?.Contains("Courier") == true ||
                        runProps?.GetFirstChild<RunFonts>()?.Ascii?.Value?.Contains("Mono") == true ||
                        runProps?.GetFirstChild<RunFonts>()?.Ascii?.Value?.Contains("Consolas") == true;
            var isStrike = runProps?.Strike != null;
            var isUnderline = runProps?.Underline != null && runProps.Underline.Val?.Value != UnderlineValues.None;

            if (isCode)
                result.Append($"`{text}`");
            else if (isBold && isItalic)
                result.Append($"***{text}***");
            else if (isBold)
                result.Append($"**{text}**");
            else if (isItalic)
                result.Append($"*{text}*");
            else if (isStrike)
                result.Append($"~~{text}~~");
            else
                result.Append(text);
        }

        return result.ToString();
    }

    private int GetHeadingLevel(string? styleId, StyleDefinitionsPart? stylesPart)
    {
        if (string.IsNullOrEmpty(styleId)) return 0;

        var lowerStyleId = styleId.ToLowerInvariant();

        // Direct match
        if (lowerStyleId.StartsWith("heading") || lowerStyleId.StartsWith("titre") || lowerStyleId.StartsWith("titlu"))
        {
            var numMatch = Regex.Match(styleId, @"\d+");
            if (numMatch.Success && int.TryParse(numMatch.Value, out var level))
                return Math.Min(level, 6);
        }

        // Check style name in styles part
        if (stylesPart?.Styles != null)
        {
            var style = stylesPart.Styles.Elements<Style>()
                .FirstOrDefault(s => s.StyleId?.Value == styleId);

            if (style?.StyleName?.Val?.Value != null)
            {
                var styleName = style.StyleName.Val.Value.ToLowerInvariant();
                var match = Regex.Match(styleName, @"heading\s*(\d+)|titre\s*(\d+)|titlu\s*(\d+)");
                if (match.Success)
                {
                    var numStr = match.Groups[1].Success ? match.Groups[1].Value :
                                 match.Groups[2].Success ? match.Groups[2].Value :
                                 match.Groups[3].Value;
                    if (int.TryParse(numStr, out var level))
                        return Math.Min(level, 6);
                }
            }
        }

        return lowerStyleId switch
        {
            "title" or "titre" or "titlu" => 1,
            "subtitle" or "soustitre" => 2,
            _ => 0
        };
    }

    private void ProcessDocxTable(Table table, StringBuilder markdown)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count == 0) return;

        markdown.AppendLine();

        // Get max columns
        var maxCols = rows.Max(r => r.Elements<TableCell>().Count());

        // First row is header
        var headerRow = rows[0];
        var headerCells = headerRow.Elements<TableCell>()
            .Select(c => c.InnerText.Trim().Replace("|", "\\|"))
            .ToList();

        while (headerCells.Count < maxCols) headerCells.Add("");

        markdown.AppendLine("| " + string.Join(" | ", headerCells) + " |");
        markdown.AppendLine("| " + string.Join(" | ", headerCells.Select(_ => "---")) + " |");

        // Data rows
        foreach (var row in rows.Skip(1))
        {
            var cells = row.Elements<TableCell>()
                .Select(c => c.InnerText.Trim().Replace("|", "\\|"))
                .ToList();

            while (cells.Count < maxCols) cells.Add("");

            markdown.AppendLine("| " + string.Join(" | ", cells) + " |");
        }

        markdown.AppendLine();
    }

    #endregion

    #region ODT to Markdown

    private async Task<ConversionResult> ConvertOdtToMarkdownAsync(byte[] odtBytes, string fileName)
    {
        var markdown = new StringBuilder();
        var metadata = new ConversionMetadata { SourceFormat = "ODT", TargetFormat = "Markdown" };

        using var memoryStream = new MemoryStream(odtBytes);
        using var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Read);

        var contentEntry = archive.GetEntry("content.xml");
        if (contentEntry == null)
        {
            return new ConversionResult
            {
                Success = false,
                ErrorMessage = "Invalid ODT document: content.xml not found"
            };
        }

        using var contentStream = contentEntry.Open();
        using var reader = new StreamReader(contentStream);
        var xmlContent = await reader.ReadToEndAsync();

        var textContent = ExtractTextFromOdtXml(xmlContent);
        markdown.Append(textContent);

        var markdownText = CleanupMarkdown(markdown.ToString());
        metadata.WordCount = CountWords(markdownText);
        metadata.CharacterCount = markdownText.Length;
        metadata.PageCount = 1;

        var outputFileName = Path.GetFileNameWithoutExtension(fileName) + ".md";

        return new ConversionResult
        {
            Success = true,
            OutputData = Encoding.UTF8.GetBytes(markdownText),
            OutputFileName = outputFileName,
            OutputMimeType = MimeTypes[".md"],
            Metadata = metadata
        };
    }

    private string ExtractTextFromOdtXml(string xmlContent)
    {
        var result = new StringBuilder();

        // Extract headings
        var headingPattern = @"<text:h[^>]*text:outline-level=""(\d+)""[^>]*>(.*?)</text:h>";
        xmlContent = Regex.Replace(xmlContent, headingPattern, m =>
        {
            var level = int.Parse(m.Groups[1].Value);
            var text = Regex.Replace(m.Groups[2].Value, @"<[^>]+>", "");
            text = System.Net.WebUtility.HtmlDecode(text);
            return $"\n{new string('#', level)} {text}\n";
        }, RegexOptions.Singleline);

        // Extract paragraphs
        var paragraphPattern = @"<text:p[^>]*>(.*?)</text:p>";
        var matches = Regex.Matches(xmlContent, paragraphPattern, RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var paragraphContent = match.Groups[1].Value;
            var cleanText = Regex.Replace(paragraphContent, @"<[^>]+>", "");
            cleanText = System.Net.WebUtility.HtmlDecode(cleanText);

            if (!string.IsNullOrWhiteSpace(cleanText))
            {
                result.AppendLine(cleanText.Trim());
                result.AppendLine();
            }
        }

        return result.ToString();
    }

    #endregion

    #region Markdown to PDF

    private Task<ConversionResult> ConvertMarkdownToPdfAsync(byte[] mdBytes, string fileName)
    {
        var markdownText = Encoding.UTF8.GetString(mdBytes);
        var metadata = new ConversionMetadata { SourceFormat = "Markdown", TargetFormat = "PDF" };

        QuestPDF.Settings.License = LicenseType.Community;

        var pdfBytes = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                page.Content()
                    .Column(column =>
                    {
                        column.Spacing(8);
                        RenderMarkdownToPdf(column, markdownText);
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
            });
        }).GeneratePdf();

        metadata.WordCount = CountWords(markdownText);
        metadata.CharacterCount = markdownText.Length;
        metadata.PageCount = 1;

        var outputFileName = Path.GetFileNameWithoutExtension(fileName) + ".pdf";

        return Task.FromResult(new ConversionResult
        {
            Success = true,
            OutputData = pdfBytes,
            OutputFileName = outputFileName,
            OutputMimeType = MimeTypes[".pdf"],
            Metadata = metadata
        });
    }

    private void RenderMarkdownToPdf(ColumnDescriptor column, string markdown)
    {
        var doc = Markdig.Markdown.Parse(markdown, _markdownPipeline);

        foreach (var block in doc)
        {
            RenderBlockToPdf(column, block);
        }
    }

    private void RenderBlockToPdf(ColumnDescriptor column, Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var headingText = GetInlineText(heading.Inline);
                var fontSize = heading.Level switch
                {
                    1 => 24,
                    2 => 20,
                    3 => 16,
                    4 => 14,
                    5 => 12,
                    _ => 11
                };
                column.Item().PaddingTop(heading.Level <= 2 ? 12 : 8)
                    .Text(headingText).FontSize(fontSize).Bold();
                break;

            case ParagraphBlock para:
                var paraText = GetInlineText(para.Inline);
                if (!string.IsNullOrWhiteSpace(paraText))
                {
                    column.Item().Text(paraText).FontSize(11);
                }
                break;

            case ListBlock list:
                RenderListToPdf(column, list, 0);
                break;

            case QuoteBlock quote:
                column.Item()
                    .BorderLeft(3)
                    .BorderColor(Colors.Grey.Medium)
                    .PaddingLeft(10)
                    .Column(quoteCol =>
                    {
                        foreach (var quoteBlock in quote)
                        {
                            RenderBlockToPdf(quoteCol, quoteBlock);
                        }
                    });
                break;

            case FencedCodeBlock code:
                var codeText = string.Join("\n", code.Lines);
                column.Item()
                    .Background(Colors.Grey.Lighten4)
                    .Padding(10)
                    .Text(codeText)
                    .FontFamily("Courier New")
                    .FontSize(10);
                break;

            case ThematicBreakBlock:
                column.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                break;

            case Markdig.Extensions.Tables.Table table:
                RenderTableToPdf(column, table);
                break;
        }
    }

    private void RenderListToPdf(ColumnDescriptor column, ListBlock list, int indent)
    {
        var index = 1;
        foreach (var item in list)
        {
            if (item is ListItemBlock listItem)
            {
                var bullet = list.IsOrdered ? $"{index}." : "•";
                var padding = indent * 15;

                column.Item().PaddingLeft(padding).Row(row =>
                {
                    row.ConstantItem(20).Text(bullet);
                    row.RelativeItem().Column(itemCol =>
                    {
                        foreach (var itemBlock in listItem)
                        {
                            if (itemBlock is ListBlock nestedList)
                            {
                                RenderListToPdf(itemCol, nestedList, indent + 1);
                            }
                            else
                            {
                                RenderBlockToPdf(itemCol, itemBlock);
                            }
                        }
                    });
                });

                index++;
            }
        }
    }

    private void RenderTableToPdf(ColumnDescriptor column, Markdig.Extensions.Tables.Table table)
    {
        column.Item().Table(pdfTable =>
        {
            var columnCount = table.Max(row => (row as Markdig.Extensions.Tables.TableRow)?.Count ?? 0);

            pdfTable.ColumnsDefinition(columns =>
            {
                for (int i = 0; i < columnCount; i++)
                {
                    columns.RelativeColumn();
                }
            });

            foreach (var row in table)
            {
                if (row is Markdig.Extensions.Tables.TableRow tableRow)
                {
                    foreach (var cell in tableRow)
                    {
                        if (cell is Markdig.Extensions.Tables.TableCell tableCell)
                        {
                            var cellText = GetBlockText(tableCell);
                            var cellItem = pdfTable.Cell()
                                .Border(1)
                                .BorderColor(Colors.Grey.Medium)
                                .Padding(5);

                            if (tableRow.IsHeader)
                            {
                                cellItem.Background(Colors.Grey.Lighten3)
                                    .Text(cellText).Bold();
                            }
                            else
                            {
                                cellItem.Text(cellText);
                            }
                        }
                    }
                }
            }
        });
    }

    private string GetInlineText(ContainerInline? inline)
    {
        if (inline == null) return "";

        var result = new StringBuilder();
        foreach (var item in inline)
        {
            result.Append(item switch
            {
                LiteralInline literal => literal.Content.ToString(),
                EmphasisInline emphasis => GetInlineText(emphasis),
                CodeInline code => code.Content,
                LinkInline link => GetInlineText(link),
                LineBreakInline => " ",
                _ => ""
            });
        }
        return result.ToString();
    }

    private string GetBlockText(Markdig.Extensions.Tables.TableCell cell)
    {
        var result = new StringBuilder();
        foreach (var block in cell)
        {
            if (block is ParagraphBlock para)
            {
                result.Append(GetInlineText(para.Inline));
            }
        }
        return result.ToString();
    }

    #endregion

    #region Markdown to DOCX

    private async Task<ConversionResult> ConvertMarkdownToDocxAsync(byte[] mdBytes, string fileName)
    {
        // Try Pandoc first for high-quality conversion
        if (_pandocConverter?.IsPandocAvailable == true)
        {
            _logger.LogDebug("Using Pandoc for Markdown to DOCX conversion");
            var pandocResult = await _pandocConverter.ConvertMarkdownToDocxAsync(mdBytes, fileName);
            if (pandocResult.Success)
                return pandocResult;

            _logger.LogWarning("Pandoc conversion failed, falling back to C# converter: {Error}", pandocResult.ErrorMessage);
        }

        // Fallback to C# implementation
        _logger.LogDebug("Using C# OpenXML for Markdown to DOCX conversion");
        var markdownText = Encoding.UTF8.GetString(mdBytes);
        var metadata = new ConversionMetadata { SourceFormat = "Markdown", TargetFormat = "DOCX" };

        using var memoryStream = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new Body());

            // Add proper styles
            AddDocxStyles(mainPart);

            // Add numbering for lists
            AddDocxNumbering(mainPart);

            // Parse markdown using Markdig
            var doc = Markdig.Markdown.Parse(markdownText, _markdownPipeline);

            foreach (var block in doc)
            {
                RenderBlockToDocx(body, mainPart, block, 0);
            }

            // Add section properties for proper page layout
            var sectPr = new SectionProperties(
                new DocumentFormat.OpenXml.Wordprocessing.PageSize() { Width = 12240, Height = 15840 }, // Letter size in twips
                new PageMargin() { Top = 1440, Bottom = 1440, Left = 1440, Right = 1440 }
            );
            body.AppendChild(sectPr);

            wordDoc.Save();
        }

        metadata.WordCount = CountWords(markdownText);
        metadata.CharacterCount = markdownText.Length;
        metadata.PageCount = 1;

        var outputFileName = Path.GetFileNameWithoutExtension(fileName) + ".docx";

        return new ConversionResult
        {
            Success = true,
            OutputData = memoryStream.ToArray(),
            OutputFileName = outputFileName,
            OutputMimeType = MimeTypes[".docx"],
            Metadata = metadata
        };
    }

    private void AddDocxStyles(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        // Normal style
        var normalStyle = new Style()
        {
            Type = StyleValues.Paragraph,
            StyleId = "Normal",
            Default = true
        };
        normalStyle.Append(new StyleName() { Val = "Normal" });
        var normalRPr = new StyleRunProperties();
        normalRPr.Append(new RunFonts() { Ascii = "Calibri", HighAnsi = "Calibri" });
        normalRPr.Append(new FontSize() { Val = "22" }); // 11pt
        normalStyle.Append(normalRPr);
        var normalPPr = new StyleParagraphProperties();
        normalPPr.Append(new SpacingBetweenLines() { After = "200", Line = "276", LineRule = LineSpacingRuleValues.Auto });
        normalStyle.Append(normalPPr);
        styles.Append(normalStyle);

        // Heading styles
        var headingSizes = new[] { 32, 26, 24, 22, 20, 18 }; // Half-points
        var headingColors = new[] { "2F5496", "2F5496", "1F3763", "1F3763", "1F3763", "1F3763" };

        for (int i = 1; i <= 6; i++)
        {
            var style = new Style()
            {
                Type = StyleValues.Paragraph,
                StyleId = $"Heading{i}"
            };
            style.Append(new StyleName() { Val = $"heading {i}" });
            style.Append(new BasedOn() { Val = "Normal" });
            style.Append(new NextParagraphStyle() { Val = "Normal" });

            var pPr = new StyleParagraphProperties();
            pPr.Append(new KeepNext());
            pPr.Append(new KeepLines());
            pPr.Append(new SpacingBetweenLines()
            {
                Before = i <= 2 ? "240" : "200",
                After = "0"
            });

            if (i == 1)
            {
                pPr.Append(new OutlineLevel() { Val = 0 });
            }

            style.Append(pPr);

            var rPr = new StyleRunProperties();
            rPr.Append(new RunFonts() { Ascii = "Calibri Light", HighAnsi = "Calibri Light" });
            rPr.Append(new Bold());
            rPr.Append(new DocumentFormat.OpenXml.Wordprocessing.Color() { Val = headingColors[i - 1] });
            rPr.Append(new FontSize() { Val = (headingSizes[i - 1] * 2).ToString() });
            style.Append(rPr);

            styles.Append(style);
        }

        // Code style
        var codeStyle = new Style()
        {
            Type = StyleValues.Character,
            StyleId = "Code"
        };
        codeStyle.Append(new StyleName() { Val = "Code" });
        var codeRPr = new StyleRunProperties();
        codeRPr.Append(new RunFonts() { Ascii = "Consolas", HighAnsi = "Consolas" });
        codeRPr.Append(new FontSize() { Val = "20" }); // 10pt
        codeRPr.Append(new Shading() { Val = ShadingPatternValues.Clear, Fill = "E7E6E6" });
        codeStyle.Append(codeRPr);
        styles.Append(codeStyle);

        stylesPart.Styles = styles;
    }

    private void AddDocxNumbering(MainDocumentPart mainPart)
    {
        var numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>();
        var numbering = new Numbering();

        // Bullet list abstract
        var bulletAbstract = new AbstractNum() { AbstractNumberId = 0 };
        for (int i = 0; i < 9; i++)
        {
            var level = new Level() { LevelIndex = i };
            level.Append(new StartNumberingValue() { Val = 1 });
            level.Append(new NumberingFormat() { Val = NumberFormatValues.Bullet });
            level.Append(new LevelText() { Val = i % 3 == 0 ? "•" : i % 3 == 1 ? "○" : "■" });
            level.Append(new LevelJustification() { Val = LevelJustificationValues.Left });
            level.Append(new PreviousParagraphProperties(
                new Indentation() { Left = ((i + 1) * 720).ToString(), Hanging = "360" }
            ));
            bulletAbstract.Append(level);
        }
        numbering.Append(bulletAbstract);

        // Numbered list abstract
        var numAbstract = new AbstractNum() { AbstractNumberId = 1 };
        for (int i = 0; i < 9; i++)
        {
            var level = new Level() { LevelIndex = i };
            level.Append(new StartNumberingValue() { Val = 1 });
            level.Append(new NumberingFormat() { Val = i % 3 == 0 ? NumberFormatValues.Decimal :
                                                        i % 3 == 1 ? NumberFormatValues.LowerLetter :
                                                        NumberFormatValues.LowerRoman });
            level.Append(new LevelText() { Val = $"%{i + 1}." });
            level.Append(new LevelJustification() { Val = LevelJustificationValues.Left });
            level.Append(new PreviousParagraphProperties(
                new Indentation() { Left = ((i + 1) * 720).ToString(), Hanging = "360" }
            ));
            numAbstract.Append(level);
        }
        numbering.Append(numAbstract);

        // Numbering instances
        numbering.Append(new NumberingInstance(new AbstractNumId() { Val = 0 }) { NumberID = 1 }); // Bullets
        numbering.Append(new NumberingInstance(new AbstractNumId() { Val = 1 }) { NumberID = 2 }); // Numbers

        numberingPart.Numbering = numbering;
    }

    private void RenderBlockToDocx(Body body, MainDocumentPart mainPart, Block block, int listLevel)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var headingPara = new Paragraph();
                headingPara.Append(new ParagraphProperties(
                    new ParagraphStyleId() { Val = $"Heading{heading.Level}" }
                ));
                AddInlineToDocx(headingPara, heading.Inline);
                body.Append(headingPara);
                break;

            case ParagraphBlock para:
                var paragraph = new Paragraph();
                AddInlineToDocx(paragraph, para.Inline);
                body.Append(paragraph);
                break;

            case ListBlock list:
                RenderListToDocx(body, mainPart, list, 0);
                break;

            case QuoteBlock quote:
                foreach (var quoteBlock in quote)
                {
                    if (quoteBlock is ParagraphBlock quotePara)
                    {
                        var qParagraph = new Paragraph();
                        var qProps = new ParagraphProperties();
                        qProps.Append(new Indentation() { Left = "720" });
                        qParagraph.Append(qProps);

                        // Add italic runs
                        if (quotePara.Inline != null)
                        {
                            foreach (var inline in quotePara.Inline)
                            {
                                var run = new Run();
                                run.Append(new RunProperties(new Italic()));
                                run.Append(new Text(GetSingleInlineText(inline)) { Space = SpaceProcessingModeValues.Preserve });
                                qParagraph.Append(run);
                            }
                        }
                        body.Append(qParagraph);
                    }
                }
                break;

            case FencedCodeBlock code:
                var codePara = new Paragraph();
                var codeProps = new ParagraphProperties();
                codeProps.Append(new Shading() { Val = ShadingPatternValues.Clear, Fill = "E7E6E6" });
                codePara.Append(codeProps);

                var codeRun = new Run();
                var codeRunProps = new RunProperties();
                codeRunProps.Append(new RunFonts() { Ascii = "Consolas", HighAnsi = "Consolas" });
                codeRunProps.Append(new FontSize() { Val = "20" });
                codeRun.Append(codeRunProps);

                foreach (var line in code.Lines)
                {
                    codeRun.Append(new Text(line.ToString()) { Space = SpaceProcessingModeValues.Preserve });
                    codeRun.Append(new Break());
                }
                codePara.Append(codeRun);
                body.Append(codePara);
                break;

            case ThematicBreakBlock:
                var hrPara = new Paragraph();
                var hrProps = new ParagraphProperties();
                hrProps.Append(new ParagraphBorders(
                    new BottomBorder() { Val = BorderValues.Single, Size = 6, Color = "888888" }
                ));
                hrPara.Append(hrProps);
                body.Append(hrPara);
                break;

            case Markdig.Extensions.Tables.Table table:
                RenderTableToDocx(body, table);
                break;
        }
    }

    private void RenderListToDocx(Body body, MainDocumentPart mainPart, ListBlock list, int level)
    {
        var numId = list.IsOrdered ? 2 : 1;

        foreach (var item in list)
        {
            if (item is ListItemBlock listItem)
            {
                foreach (var itemBlock in listItem)
                {
                    if (itemBlock is ParagraphBlock itemPara)
                    {
                        var para = new Paragraph();
                        var paraProps = new ParagraphProperties();

                        var numProps = new NumberingProperties();
                        numProps.Append(new NumberingLevelReference() { Val = level });
                        numProps.Append(new NumberingId() { Val = numId });
                        paraProps.Append(numProps);

                        para.Append(paraProps);
                        AddInlineToDocx(para, itemPara.Inline);
                        body.Append(para);
                    }
                    else if (itemBlock is ListBlock nestedList)
                    {
                        RenderListToDocx(body, mainPart, nestedList, level + 1);
                    }
                }
            }
        }
    }

    private void RenderTableToDocx(Body body, Markdig.Extensions.Tables.Table mdTable)
    {
        var table = new Table();

        // Table properties
        var tblProps = new TableProperties();
        tblProps.Append(new TableBorders(
            new TopBorder() { Val = BorderValues.Single, Size = 4 },
            new BottomBorder() { Val = BorderValues.Single, Size = 4 },
            new LeftBorder() { Val = BorderValues.Single, Size = 4 },
            new RightBorder() { Val = BorderValues.Single, Size = 4 },
            new InsideHorizontalBorder() { Val = BorderValues.Single, Size = 4 },
            new InsideVerticalBorder() { Val = BorderValues.Single, Size = 4 }
        ));
        tblProps.Append(new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct }); // 100%
        table.Append(tblProps);

        foreach (var row in mdTable)
        {
            if (row is Markdig.Extensions.Tables.TableRow mdRow)
            {
                var tableRow = new TableRow();

                foreach (var cell in mdRow)
                {
                    if (cell is Markdig.Extensions.Tables.TableCell mdCell)
                    {
                        var tableCell = new TableCell();

                        // Cell properties
                        var tcProps = new TableCellProperties();
                        if (mdRow.IsHeader)
                        {
                            tcProps.Append(new Shading() { Val = ShadingPatternValues.Clear, Fill = "D9E2F3" });
                        }
                        tableCell.Append(tcProps);

                        // Cell content
                        var cellPara = new Paragraph();
                        if (mdRow.IsHeader)
                        {
                            var boldRun = new Run();
                            boldRun.Append(new RunProperties(new Bold()));
                            boldRun.Append(new Text(GetBlockText(mdCell)) { Space = SpaceProcessingModeValues.Preserve });
                            cellPara.Append(boldRun);
                        }
                        else
                        {
                            cellPara.Append(new Run(new Text(GetBlockText(mdCell)) { Space = SpaceProcessingModeValues.Preserve }));
                        }
                        tableCell.Append(cellPara);

                        tableRow.Append(tableCell);
                    }
                }

                table.Append(tableRow);
            }
        }

        body.Append(table);
        body.Append(new Paragraph()); // Space after table
    }

    private void AddInlineToDocx(Paragraph para, ContainerInline? inline)
    {
        if (inline == null) return;

        foreach (var item in inline)
        {
            AddSingleInlineToDocx(para, item);
        }
    }

    private void AddSingleInlineToDocx(Paragraph para, Inline item)
    {
        switch (item)
        {
            case LiteralInline literal:
                var literalRun = new Run(new Text(literal.Content.ToString()) { Space = SpaceProcessingModeValues.Preserve });
                para.Append(literalRun);
                break;

            case EmphasisInline emphasis:
                foreach (var child in emphasis)
                {
                    var emphRun = new Run();
                    var emphProps = new RunProperties();

                    if (emphasis.DelimiterCount == 2 || (emphasis.DelimiterCount == 1 && emphasis.DelimiterChar == '*'))
                    {
                        if (emphasis.DelimiterCount >= 2)
                            emphProps.Append(new Bold());
                        if (emphasis.DelimiterCount == 1 || emphasis.DelimiterCount == 3)
                            emphProps.Append(new Italic());
                    }

                    emphRun.Append(emphProps);
                    emphRun.Append(new Text(GetSingleInlineText(child)) { Space = SpaceProcessingModeValues.Preserve });
                    para.Append(emphRun);
                }
                break;

            case CodeInline code:
                var codeRun = new Run();
                var codeProps = new RunProperties();
                codeProps.Append(new RunFonts() { Ascii = "Consolas", HighAnsi = "Consolas" });
                codeProps.Append(new Shading() { Val = ShadingPatternValues.Clear, Fill = "E7E6E6" });
                codeRun.Append(codeProps);
                codeRun.Append(new Text(code.Content) { Space = SpaceProcessingModeValues.Preserve });
                para.Append(codeRun);
                break;

            case LinkInline link:
                var linkText = GetInlineText(link);
                var linkRun = new Run();
                var linkProps = new RunProperties();
                linkProps.Append(new DocumentFormat.OpenXml.Wordprocessing.Color() { Val = "0563C1" });
                linkProps.Append(new Underline() { Val = UnderlineValues.Single });
                linkRun.Append(linkProps);
                linkRun.Append(new Text(linkText) { Space = SpaceProcessingModeValues.Preserve });
                para.Append(linkRun);
                break;

            case LineBreakInline:
                para.Append(new Run(new Break()));
                break;
        }
    }

    private string GetSingleInlineText(Inline item)
    {
        return item switch
        {
            LiteralInline literal => literal.Content.ToString(),
            EmphasisInline emphasis => GetInlineText(emphasis),
            CodeInline code => code.Content,
            LinkInline link => GetInlineText(link),
            _ => ""
        };
    }

    #endregion

    #region Helpers

    private int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    #endregion
}
