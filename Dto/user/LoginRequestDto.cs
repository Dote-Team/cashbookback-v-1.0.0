using System.ComponentModel;

namespace cashbook.Dto.user;

public class LoginRequestDto
{
	/// <summary>اسم الدخول. أرسله أو أرسل Email.</summary>
	[DefaultValue("admin")]
	public string? Username { get; set; }

	/// <summary>البريد بدل اسم الدخول.</summary>
	[DefaultValue("admin")]
	public string? Email { get; set; }

	/// <summary>كلمة المرور.</summary>
	[DefaultValue("admin")]
	public string Password { get; set; }

	/// <summary>اختياري: معرّف الجهاز، يظهر في سجل التدقيق.</summary>
	public string? DeviceToken { get; set; }
}
