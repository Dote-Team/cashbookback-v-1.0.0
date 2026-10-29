using System;

namespace cashbook.Dto.contact;

public class UpdateContactDto
{
	/// <summary>الاسم الجديد.</summary>
	public string? Name { get; set; }

	/// <summary>الهاتف الجديد. نص فارغ يمسحه، وعدم إرساله يبقيه.</summary>
	public string? Phone { get; set; }

	public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
