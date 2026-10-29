using System;

namespace cashbook.Dto.book;

public class UpdateBookDto
{
	/// <summary>الاسم الجديد.</summary>
	public string? Name { get; set; }

	/// <summary>نقل إلى منشأة أخرى (للسوبر أدمن فقط). اتركه فارغاً للإبقاء.</summary>
	public Guid BusinessId { get; set; }
}
