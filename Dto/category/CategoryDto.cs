using cashbook.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace cashbook.Dto.category
{
    public class CategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Guid BusinessId { get; set; }
        public DateTime CreatedAt { get; set; } 
        public DateTime UpdatedAt { get; set; }
    }
}
