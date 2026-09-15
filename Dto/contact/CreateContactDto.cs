namespace cashbook.Dto.contact
{
    public class CreateContactDto
    {
        public string Name { get; set; }
        public string Phone { get; set; }
        public Guid BusinessId { get; set; }
    }
}
