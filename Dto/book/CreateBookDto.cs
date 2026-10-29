using System;

namespace cashbook.Dto.book;

public class CreateBookDto
{
	/// <summary>اسم الخزنة.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>المنشأة التي تتبعها.</summary>
	public Guid BusinessId { get; set; }
}
