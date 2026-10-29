using System;

namespace cashbook.Dto.business;

public class UpdateBusinessDto
{
	/// <summary>الاسم الجديد.</summary>
	public string? Name { get; set; }

	public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
