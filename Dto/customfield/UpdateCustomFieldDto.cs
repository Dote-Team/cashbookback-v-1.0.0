namespace cashbook.Dto.customfield;

public class UpdateCustomFieldDto
{
	/// <summary>الاسم الجديد.</summary>
	public string? Key { get; set; }

	/// <summary>إلزامي أم لا.</summary>
	public bool? IsRequired { get; set; }
}
