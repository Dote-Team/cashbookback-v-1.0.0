using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace cashbook.Models
{
    public class BusinessUser
    {
        [Required]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        [Required]
        [ForeignKey("User")]
        public Guid UserId { get; set; }
        public User User { get; set; }
        [Required]
        [ForeignKey("Business")]
        public Guid BusinessId { get; set; }
        public Business Business { get; set; }
        public List<Guid>? BookIds { get; set; }
        public string Role { get; set; }
    }
}
