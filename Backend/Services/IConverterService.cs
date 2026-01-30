namespace MDConverter360.Services;

public interface IConverterService
{
    Task<ConversionResult> ConvertAsync(Stream inputStream, string inputFileName, ConversionType conversionType);
    Task<ConversionResult> ConvertAsync(byte[] inputBytes, string inputFileName, ConversionType conversionType);
    ConversionType DetectConversionType(string inputFileName, string? targetFormat = null);
    bool IsSupported(string fileName);
}

public enum ConversionType
{
    PdfToMarkdown,
    DocxToMarkdown,
    OdtToMarkdown,
    MarkdownToPdf,
    MarkdownToDocx
}

public class ConversionResult
{
    public bool Success { get; set; }
    public byte[]? OutputData { get; set; }
    public string? OutputFileName { get; set; }
    public string? OutputMimeType { get; set; }
    public string? ErrorMessage { get; set; }
    public ConversionMetadata? Metadata { get; set; }
}

public class ConversionMetadata
{
    public int PageCount { get; set; }
    public int WordCount { get; set; }
    public int CharacterCount { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public string? SourceFormat { get; set; }
    public string? TargetFormat { get; set; }
}
