using cashbook.Dto.book;
using cashbook.Dto.user;

namespace cashbook.Dto.businessUser
{
    public class UpdateBusinessUserDto
    {
        public Guid? BusinessId { get; set; }
        public string? Role { get; set; }

        public List<Guid>? BookIds { get; set; }
    }
}
