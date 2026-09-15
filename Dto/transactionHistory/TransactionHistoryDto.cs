using System;
using cashbook.Dto.book;
using cashbook.Dto.user;

namespace cashbook.Dto.transactionHistory;

public class TransactionHistoryDto
{
	public Guid Id { get; set; }

	public string Operation { get; set; }

	public string Description { get; set; }

	public string Type { get; set; }

	public decimal From { get; set; }

	public decimal To { get; set; }

	public decimal Amount { get; set; }

	public decimal? ExchangeRate { get; set; }

	public DateTime? ExchangeDate { get; set; }

	public Guid BookId { get; set; }

	public Guid? TransactionId { get; set; }

	public Guid UserId { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }

	public UserDto? User { get; set; }

	public BookDto? Book { get; set; }
}
