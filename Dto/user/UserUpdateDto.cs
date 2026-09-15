namespace cashbook.Dto.user
{
    public class UserUpdateDto
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? Name { get; set; }
        public IFormFile? ProfileImage { get; set; }

    }



}
