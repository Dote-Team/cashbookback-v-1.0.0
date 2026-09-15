using System;

namespace cashbook.Dto.Book;

public class CustomFieldDto
{
	public Guid Id { get; set; }

	public string Key { get; set; }

	public Guid BookId { get; set; }

	public bool IsRequired { get; set; }
}
