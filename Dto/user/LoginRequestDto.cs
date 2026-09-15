
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace cashbook.Dto.user
{
    public class LoginRequestDto
    {
        [DefaultValue("admin")]
        public string Email { get; set; }

        [DefaultValue("admin")]
        public string Password { get; set; }
        public string DeviceToken { get; set; }
    }
}
