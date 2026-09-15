using System;
using System.Collections.Generic;

namespace cashbook.Dto.user;

public class LoginResponseDto
{
	public string Email { get; set; }

	public string Username { get; set; }

	public string AccessToken { get; set; }

	public string RefreshToken { get; internal set; }

	public Guid SessionId { get; set; }

	public string Name { get; set; }

	public string? ProfileImage { get; set; }

	public Guid Id { get; set; }

	public bool IsSuperAdmin { get; set; }

	public bool CanManageExchangeRate { get; set; }

	public List<Guid> ExchangeRateBusinessIds { get; set; } = new List<Guid>();
}
