using System;

namespace cashbook.Dto.Book;

public class AttachmentDto
{
	public Guid Id { get; set; }

	public string Files { get; set; }

	public Guid TransactionId { get; set; }
}
