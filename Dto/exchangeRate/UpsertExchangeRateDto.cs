using System;
using cashbook.Models.Enums;

namespace cashbook.Dto.exchangeRate;

public class UpsertExchangeRateDto
{
	public Guid BookId { get; set; }

	public CurrencyCode Currency { get; set; } = CurrencyCode.USD;

	public decimal Rate { get; set; }

	public DateTime? RateDate { get; set; }
}
