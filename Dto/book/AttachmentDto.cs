using System;

namespace cashbook.Dto.Book;

public class AttachmentDto
{
	public Guid Id { get; set; }

	/// <summary>اسم الملف مع توقيع مؤقت، يُلحق بـ /API/Files/ كما هو.</summary>
	public string Files { get; set; } = string.Empty;

	/// <summary>اسم الملف المخزن، بلا توقيع.</summary>
	public string? FileName { get; set; }

	public Guid TransactionId { get; set; }
}
