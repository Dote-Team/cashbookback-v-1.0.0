namespace cashbook.Dto.user
{
    public class InviteDto
    {
        public string Email { get; set; }
        public string Role { get; set; }
        public Guid BusinessId { get; set; }
        public List<Guid>? BookIds { get; set; }  

    }
}
