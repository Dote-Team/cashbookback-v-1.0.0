using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace cashbook.Models
{
    public class PaymentMethod
    {
        [Required]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string Name { get; set; }
        [Required]
        [ForeignKey("Business")]
        public Guid BusinessId { get; set; }
        public Business Business { get; set; }
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public ICollection<Transaction> Transactions { get; set; }
    }
}
