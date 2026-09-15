using Microsoft.AspNetCore.Http;

namespace cashbook.Dto.user;

public class UserUpdateDto
{
	public string? Username { get; set; }

	public string? Email { get; set; }

	public string? Password { get; set; }

	public string? Name { get; set; }

	public IFormFile? ProfileImage { get; set; }
}
