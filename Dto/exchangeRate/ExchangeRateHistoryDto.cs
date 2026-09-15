using System;
using cashbook.Models.Enums;

namespace cashbook.Dto.exchangeRate;

public class ExchangeRateHistoryDto
{
	public Guid Id { get; set; }

	public Guid BookId { get; set; }

	public CurrencyCode Currency { get; set; }

	public decimal Rate { get; set; }

	public DateTime RateDate { get; set; }

	public Guid? SetByUserId { get; set; }

	public string? SetByUserName { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}
