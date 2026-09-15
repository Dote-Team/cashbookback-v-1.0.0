
using System.ComponentModel;

namespace cashbook.Dto.user
{
    public class RegisterationRequestDto
    {
        [DefaultValue("admin")]
        public string Email { get; set; }
        [DefaultValue("admin")]
        public string Password { get; set; }
        [DefaultValue("admin")]
        public string Name { get; set; }


    }


}
