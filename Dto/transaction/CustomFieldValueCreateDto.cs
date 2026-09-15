using System;

namespace cashbook.Dto.transaction;

public class CustomFieldValueCreateDto
{
	public string Value { get; set; }

	public Guid CustomFieldId { get; set; }
}
