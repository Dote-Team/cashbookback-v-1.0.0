using cashbook.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using cashbook.Dto.business;
using cashbook.Dto.user;
using cashbook.Dto.book;

namespace cashbook.Dto.transactionHistory
{
    public class TransactionHistoryDto
    {
        public Guid Id { get; set; }
        public string Operation { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public decimal From { get; set; }
        public decimal To { get; set; }
        public decimal Amount { get; set; }
        public Guid BookId { get; set; }
        public Guid TransactionId { get; set; }
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public UserDto? User { get; set; }
        public BookDto? Book { get; set; }

    }
}
