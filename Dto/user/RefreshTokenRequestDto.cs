namespace cashbook.Dto.user;

public class RefreshTokenRequestDto
{
	/// <summary>رمز التحديث من آخر دخول أو تجديد.</summary>
	public string RefreshToken { get; set; } = string.Empty;
}
