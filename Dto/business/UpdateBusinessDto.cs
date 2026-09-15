using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace cashbook.Dto.business
{
    public class UpdateBusinessDto
    {
        
        public string? Name { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
