using System;

namespace cashbook.Dto.category;

public class CategoryDto
{
	public Guid Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public Guid BusinessId { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}
