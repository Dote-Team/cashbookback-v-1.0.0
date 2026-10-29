using System;

namespace cashbook.Dto.Trash;

public class TrashItemDto
{
	public Guid Id { get; set; }

	public string EntityType { get; set; }

	public Guid EntityId { get; set; }

	public string? Name { get; set; }

	public Guid? BusinessId { get; set; }

	public Guid? BookId { get; set; }

	public DateTime DeletedAt { get; set; }
}
