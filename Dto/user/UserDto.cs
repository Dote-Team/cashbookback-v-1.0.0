

namespace cashbook.Dto.user
{
    public class UserDto
    {
        public Guid Id { get; set; }

        public string Email { get; set; }

        public string Name { get; set; }
        public string Role { get; set; }
        public string BusinessName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

    }


}
