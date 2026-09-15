using System;

namespace cashbook.Dto.book;

public class BookDto
{
	public Guid Id { get; set; }

	public string Name { get; set; }

	public Guid BusinessId { get; set; }

	public decimal Balance { get; set; }

	public decimal BalanceIqd { get; set; }

	public decimal BalanceUsd { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}
