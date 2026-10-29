using System;

namespace cashbook.Dto.contact;

public class CreateContactDto
{
	/// <summary>اسم جهة الاتصال.</summary>
	public string Name { get; set; }

	/// <summary>الهاتف (اختياري).</summary>
	public string Phone { get; set; }

	/// <summary>المنشأة.</summary>
	public Guid BusinessId { get; set; }
}
