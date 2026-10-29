using System;

namespace cashbook.Dto.business;

public class BusinessDto
{
	public Guid Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }

	public string? Role { get; set; }

	public int BooksCount { get; set; }
}
