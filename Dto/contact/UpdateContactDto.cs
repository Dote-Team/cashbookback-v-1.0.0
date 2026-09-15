namespace cashbook.Dto.contact
{
    public class UpdateContactDto
    {
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
