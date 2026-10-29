using System;
using System.Text.Json.Serialization;

namespace cashbook.Dto.category;

public class UpdateCategoryDto
{
	/// <summary>الاسم الجديد.</summary>
	public string? Name { get; set; }

	[JsonIgnore]
	public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
