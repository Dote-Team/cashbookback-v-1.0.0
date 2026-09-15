using System;
using System.Collections.Generic;

namespace cashbook.Dto.businessUser;

public class UpdateBusinessUserDto
{
	public Guid? BusinessId { get; set; }

	public string? Role { get; set; }

	public List<Guid>? BookIds { get; set; }
}
