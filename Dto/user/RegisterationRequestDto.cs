using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace cashbook.Dto.user;

public class RegisterationRequestDto
{
	[Required(ErrorMessage = "Username is required")]
	[DefaultValue("admin")]
	public string Username { get; set; } = string.Empty;

	[DefaultValue("admin")]
	public string? Email { get; set; }

	[DefaultValue("admin")]
	public string Password { get; set; } = string.Empty;

	[DefaultValue("admin")]
	public string Name { get; set; } = string.Empty;
}
