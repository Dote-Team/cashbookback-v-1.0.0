using System;
using cashbook.Models.Enums;

namespace cashbook.Dto.exchangeRate;

public class CurrentExchangeRateDto
{
	public Guid BookId { get; set; }

	public CurrencyCode Currency { get; set; }

	public decimal? Rate { get; set; }

	public DateTime? RateDate { get; set; }

	public bool IsToday { get; set; }

	public bool HasValue { get; set; }
}
