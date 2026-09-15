namespace cashbook.Dto.customfield
{
    public class CreateCustomFieldDto
    {
        public string Key { get; set; }
        public Guid BookId { get; set; }
        public bool IsRequired { get; set; }
    }
}
