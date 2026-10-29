using System;
using System.Collections.Generic;

namespace cashbook.Dto.user;

public class InviteDto
{
	public string Email { get; set; } = string.Empty;

	public string Role { get; set; } = string.Empty;

	public Guid BusinessId { get; set; }

	public List<Guid>? BookIds { get; set; }
}
