# MD.converter360 - Document Converter

## Overview

**MD.converter360** is a document format converter application that transforms documents between PDF, Word (DOCX/DOC/ODT), and Markdown formats. Part of the 360 Suite.

## Features

- **PDF → Markdown**: Extract text and structure from PDF files
- **Word → Markdown**: Convert DOCX/DOC/ODT to Markdown with formatting preservation
- **Markdown → PDF**: Generate professional PDFs from Markdown files
- **Markdown → DOCX**: Create Word documents from Markdown
- **Drag & Drop**: Easy file upload with multiple file support
- **Batch Conversion**: Convert multiple files at once
- **Auto-save to Downloads**: Converted files automatically saved to Downloads folder
- **Dark/Light Mode**: Theme toggle for user preference

## Tech Stack

### Backend (.NET 10)
- ASP.NET Core Web API
- **PdfPig** - PDF text extraction
- **DocumentFormat.OpenXml** - Word document handling
- **Markdig** - Markdown processing
- **QuestPDF** - PDF generation
- **Serilog** - Logging

### Frontend (React 19 + Vite)
- React 19 with Hooks
- Axios for API calls
- Lucide React for icons
- CSS Variables for theming

## Port Allocation

| Component | Port | URL |
|-----------|------|-----|
| Backend | 5294 | http://localhost:5294 |
| Frontend | 5172 | http://localhost:5172 |
| Swagger | 5294 | http://localhost:5294/swagger |

## Quick Start

```bash
# Option 1: Double-click Start.bat
# Option 2: Run from command line
cd D:\AI_projects\MD.converter360
Start.bat
```

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/conversion/convert` | POST | Convert single file |
| `/api/conversion/convert-batch` | POST | Convert multiple files |
| `/api/conversion/formats` | GET | Get supported formats |
| `/api/conversion/health` | GET | Health check |
| `/api/health` | GET | Service health |

### Convert Single File

```bash
curl -X POST http://localhost:5294/api/conversion/convert \
  -F "file=@document.pdf" \
  -F "targetFormat=md" \
  -F "saveToDownloads=true"
```

### Convert Multiple Files

```bash
curl -X POST http://localhost:5294/api/conversion/convert-batch \
  -F "files=@doc1.pdf" \
  -F "files=@doc2.docx" \
  -F "saveToDownloads=true"
```

## Supported Formats

### Input Formats
| Format | Extension | Description |
|--------|-----------|-------------|
| PDF | .pdf | Portable Document Format |
| Word | .docx | Microsoft Word 2007+ |
| Word Legacy | .doc | Microsoft Word 97-2003 |
| OpenDocument | .odt | OpenDocument Text |
| Markdown | .md, .markdown | Markdown text |

### Output Formats
| Format | Extension | MIME Type |
|--------|-----------|-----------|
| Markdown | .md | text/markdown |
| PDF | .pdf | application/pdf |
| Word | .docx | application/vnd.openxmlformats-officedocument.wordprocessingml.document |

## Project Structure

```
MD.converter360/
├── Backend/
│   ├── Controllers/
│   │   └── ConversionController.cs     # API endpoints
│   ├── Services/
│   │   ├── IConverterService.cs        # Service interface
│   │   └── ConverterService.cs         # Conversion logic
│   ├── Program.cs                       # App configuration
│   └── MDConverter360.csproj            # Project file
├── Frontend/
│   ├── src/
│   │   ├── App.jsx                      # Main component
│   │   └── App.css                      # Styles
│   ├── vite.config.js                   # Vite configuration
│   └── package.json                     # NPM dependencies
├── Start.bat                            # Start script
├── Stop.bat                             # Stop script
├── CreateDesktopShortcut.ps1            # Shortcut creator
└── CLAUDE.md                            # This file
```

## Development

### Build Backend
```bash
cd Backend
dotnet build
dotnet run
```

### Build Frontend
```bash
cd Frontend
npm install
npm run dev
```

### Run Tests
```bash
cd Backend
dotnet test
```

## Conversion Quality Notes

### PDF to Markdown
- Text extraction preserves basic structure
- Headers detected by font size and capitalization
- Tables converted to Markdown table format
- Complex layouts may require manual adjustment

### Word to Markdown
- Heading styles (H1-H6) preserved
- Bold, italic, and inline code formatting
- Tables converted with proper alignment
- Lists (bulleted and numbered) supported

### Markdown to PDF
- Professional PDF generation with QuestPDF
- Page headers and footers
- Syntax highlighting for code blocks
- Table support

### Markdown to DOCX
- Heading styles applied
- Inline formatting preserved
- Paragraph spacing maintained

## Troubleshooting

### Backend won't start
1. Check if port 5294 is available
2. Ensure .NET 10 SDK is installed
3. Check `Backend/Logs/` for error details

### Frontend won't start
1. Check if port 5172 is available
2. Run `npm install` in Frontend folder
3. Check console for npm errors

### Conversion fails
1. Check file format is supported
2. Ensure file is not corrupted
3. Check backend logs for details

## Related Libraries

- [Marker](https://github.com/datalab-to/marker) - PDF to Markdown (Python)
- [MinerU](https://github.com/opendatalab/MinerU) - Document transformation
- [Pandoc](https://pandoc.org/) - Universal document converter
- [MarkItDown](https://github.com/microsoft/markitdown) - Microsoft's converter

## Version History

### v1.0.0 (2026-01-29)
- Initial release
- PDF, DOCX, ODT to Markdown conversion
- Markdown to PDF and DOCX conversion
- Drag & drop interface
- Batch conversion support
- Auto-save to Downloads
- Dark/Light theme toggle

## License

Part of the 360 Suite - Internal use only.
