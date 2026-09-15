
namespace cashbook.Dto.user
{
    public class LoginResponseDto
    {
        public string Email { get; set; }
        //public string Role { get; internal set; }
        //public Guid BusinessId { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; internal set; }
        public Guid SessionId { get; set; }
        public string Name { get; set; }
        public string? ProfileImage { get; set; }
        public Guid Id { get; set; }
    }
}
