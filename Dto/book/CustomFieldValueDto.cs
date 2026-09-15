using System;

namespace cashbook.Dto.Book;

public class CustomFieldValueDto
{
	public Guid Id { get; set; }

	public string Value { get; set; }

	public Guid CustomFieldId { get; set; }

	public Guid TransactionId { get; set; }

	public CustomFieldDto? CustomFields { get; set; }
}
