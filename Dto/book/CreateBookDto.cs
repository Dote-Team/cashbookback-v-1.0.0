using System;

namespace cashbook.Dto.book;

public class CreateBookDto
{
	public string Name { get; set; }

	public Guid BusinessId { get; set; }
}
