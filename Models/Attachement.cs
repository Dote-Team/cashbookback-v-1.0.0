using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace cashbook.Models
{
    public class Attachement
    {
        [Required]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        [Required]
        public string Files { get; set; }
        [Required]
        [ForeignKey("Transaction")]
        public Guid TransactionId { get; set; }
        public Transaction Transaction { get; set; }
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        //public ICollection<Transaction> Transactions { get; set; }


    }
}
