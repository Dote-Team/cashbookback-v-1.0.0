using Microsoft.AspNetCore.Http;

namespace cashbook.Dto.user;

public class UserUpdateDto
{
	/// <summary>اسم دخول جديد.</summary>
	public string? Username { get; set; }

	/// <summary>بريد جديد.</summary>
	public string? Email { get; set; }

	/// <summary>كلمة مرور جديدة: 8 أحرف على الأقل، فيها حرف ورقم.</summary>
	public string? Password { get; set; }

	/// <summary>الاسم الظاهر.</summary>
	public string? Name { get; set; }

	/// <summary>صورة شخصية: JPG أو PNG أو GIF أو WEBP، حتى 10MB.</summary>
	public IFormFile? ProfileImage { get; set; }
}
