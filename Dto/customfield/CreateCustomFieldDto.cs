using System;

namespace cashbook.Dto.customfield;

public class CreateCustomFieldDto
{
	/// <summary>اسم الحقل، مثل «رقم الفاتورة».</summary>
	public string Key { get; set; } = string.Empty;

	/// <summary>الخزنة.</summary>
	public Guid BookId { get; set; }

	/// <summary>true = إلزامي عند إضافة قيد.</summary>
	public bool IsRequired { get; set; }
}
