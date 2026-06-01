using System.Text;
using AIDebateStudio.Models;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace AIDebateStudio.Services;

public sealed class FileParsingService
{
	private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".txt", ".md", ".markdown", ".csv", ".json", ".xml", ".yaml", ".yml", ".log", ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".h", ".html", ".css"
	};

	public async Task<AttachmentContext> ParseAsync(FileResult file, CancellationToken cancellationToken)
	{
		var extension = Path.GetExtension(file.FileName);
		await using var stream = await file.OpenReadAsync();
		await using var memory = new MemoryStream();
		await stream.CopyToAsync(memory, cancellationToken);
		var bytes = memory.ToArray();

		var content = extension.ToLowerInvariant() switch
		{
			".pdf" => ExtractPdf(bytes),
			".docx" => ExtractDocx(bytes),
			".pptx" => ExtractPptx(bytes),
			".xlsx" => ExtractXlsx(bytes),
			_ when TextExtensions.Contains(extension) => DecodeText(bytes),
			_ => $"暂不支持直接解析 {extension} 文件。可以先转为 txt、md、pdf、docx、pptx、xlsx，或使用 OCR 入口处理图片。"
		};

		return new AttachmentContext
		{
			FileName = file.FileName,
			Kind = extension.TrimStart('.').ToUpperInvariant(),
			Content = Normalize(content)
		};
	}

	private static string ExtractPdf(byte[] bytes)
	{
		using var stream = new MemoryStream(bytes);
		using var document = PdfDocument.Open(stream);
		var builder = new StringBuilder();
		foreach (var page in document.GetPages())
		{
			builder.AppendLine($"--- 第 {page.Number} 页 ---");
			builder.AppendLine(page.Text);
		}

		return builder.ToString();
	}

	private static string ExtractDocx(byte[] bytes)
	{
		using var stream = new MemoryStream(bytes);
		using var document = WordprocessingDocument.Open(stream, false);
		return document.MainDocumentPart?.Document.Body?.InnerText ?? string.Empty;
	}

	private static string ExtractPptx(byte[] bytes)
	{
		using var stream = new MemoryStream(bytes);
		using var document = PresentationDocument.Open(stream, false);
		var builder = new StringBuilder();
		var index = 1;
		foreach (var slidePart in document.PresentationPart?.SlideParts ?? [])
		{
			builder.AppendLine($"--- 第 {index++} 页幻灯片 ---");
			builder.AppendLine(slidePart.Slide.InnerText);
		}

		return builder.ToString();
	}

	private static string ExtractXlsx(byte[] bytes)
	{
		using var stream = new MemoryStream(bytes);
		using var document = SpreadsheetDocument.Open(stream, false);
		var sharedStrings = document.WorkbookPart?.SharedStringTablePart?.SharedStringTable
			.Elements<DocumentFormat.OpenXml.Spreadsheet.SharedStringItem>()
			.Select(item => item.InnerText)
			.ToArray() ?? [];
		var builder = new StringBuilder();

		foreach (var worksheetPart in document.WorkbookPart?.WorksheetParts ?? [])
		{
			foreach (var row in worksheetPart.Worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.Row>())
			{
				var cells = row.Elements<DocumentFormat.OpenXml.Spreadsheet.Cell>().Select(cell =>
				{
					var value = cell.CellValue?.Text ?? string.Empty;
					if (cell.DataType?.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.SharedString &&
						int.TryParse(value, out var sharedIndex) &&
						sharedIndex >= 0 &&
						sharedIndex < sharedStrings.Length)
					{
						return sharedStrings[sharedIndex];
					}

					return value;
				});
				builder.AppendLine(string.Join("\t", cells));
			}
		}

		return builder.ToString();
	}

	private static string DecodeText(byte[] bytes)
	{
		var utf8 = new UTF8Encoding(false, true);
		try
		{
			return utf8.GetString(bytes);
		}
		catch (DecoderFallbackException)
		{
			return Encoding.Default.GetString(bytes);
		}
	}

	private static string Normalize(string content)
	{
		var value = content.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
		return value.Length > 30000 ? value[..30000] + "\n...[文件内容较长，已截断到 30000 字符]" : value;
	}
}
