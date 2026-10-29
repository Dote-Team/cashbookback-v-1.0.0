using System;

namespace cashbook.Dto.category;

public class CreateCategoryDto
{
	/// <summary>اسم التصنيف.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>المنشأة.</summary>
	public Guid BusinessId { get; set; }
}
