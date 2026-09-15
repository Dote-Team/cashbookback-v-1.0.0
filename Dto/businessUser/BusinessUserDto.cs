using System;
using System.Collections.Generic;
using cashbook.Dto.book;
using cashbook.Dto.business;
using cashbook.Dto.user;

namespace cashbook.Dto.businessUser;

public class BusinessUserDto
{
	public Guid Id { get; set; }

	public Guid UserId { get; set; }

	public Guid BusinessId { get; set; }

	public string Role { get; set; }

	public UserDto User { get; set; }

	public List<BookDto> Books { get; set; }

	public BusinessDto Business { get; set; }
}
