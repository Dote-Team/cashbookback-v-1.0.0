using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace cashbook.Models
{
    public class Session
    {
        [Required]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        [Required]
        public string RefreshToken { get; set; }
        [Required]
        public string DeviceToken { get; set; }
        [Required]
        [ForeignKey("User")]
        public Guid UserId { get; set; }
        public User User { get; set; }
        public DateTime ExpiredAt { get; set; }
    }
}
