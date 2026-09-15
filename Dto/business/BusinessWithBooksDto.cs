using System;
using System.Collections.Generic;
using cashbook.Dto.book;

namespace cashbook.Dto.business;

public class BusinessWithBooksDto
{
	public Guid Id { get; set; }

	public string Name { get; set; }

	public List<BookDto> Books { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}
