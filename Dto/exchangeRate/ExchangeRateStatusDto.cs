using System;
using cashbook.Models.Enums;

namespace cashbook.Dto.exchangeRate;

public class ExchangeRateStatusDto
{
	public Guid BookId { get; set; }

	public string BookName { get; set; }

	public CurrencyCode Currency { get; set; }

	public DateTime RateDate { get; set; }

	public decimal? Rate { get; set; }

	public bool IsSet { get; set; }
}
