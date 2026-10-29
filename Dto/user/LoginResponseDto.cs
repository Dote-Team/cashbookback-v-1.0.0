using System;
using System.Collections.Generic;

namespace cashbook.Dto.user;

public class LoginResponseDto
{
	public string Email { get; set; } = string.Empty;

	public string Username { get; set; } = string.Empty;

	public string AccessToken { get; set; } = string.Empty;

	public string RefreshToken { get; internal set; } = string.Empty;

	public Guid SessionId { get; set; }

	public string Name { get; set; } = string.Empty;

	public string? ProfileImage { get; set; }

	public Guid Id { get; set; }

	public bool IsSuperAdmin { get; set; }

	/// <summary>1 = سوبر أدمن، 2 = أدمن، 3 = مستخدم.</summary>
	public int Level { get; set; }

	public string LevelName { get; set; } = string.Empty;

	public bool CanManageExchangeRate { get; set; }

	public List<Guid> ExchangeRateBusinessIds { get; set; } = new List<Guid>();
}
