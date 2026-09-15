using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace cashbook.Dto.customfield
{
    public class CustomFieldDto
    {
        public Guid Id { get; set; }
        public string Key { get; set; }
        public Guid BookId { get; set; }
        public bool IsRequired { get; set; }
    }
}
