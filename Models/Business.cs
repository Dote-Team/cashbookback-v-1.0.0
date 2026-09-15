using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace cashbook.Models
{
    public class Business
    {
        [Required]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public ICollection<Category> Categories { get; set; }
        public ICollection<PaymentMethod> PaymentMethods { get; set; }
        public ICollection<Contact> Contacts { get; set; }
        public ICollection<Book> Books { get; set; }
        public ICollection<BusinessUser> BusinessUsers { get; set; }
    }
}
