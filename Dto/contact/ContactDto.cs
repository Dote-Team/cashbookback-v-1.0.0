using System;

namespace cashbook.Dto.contact;

public class ContactDto
{
	public Guid Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public string Phone { get; set; } = string.Empty;

	public Guid BusinessId { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}
