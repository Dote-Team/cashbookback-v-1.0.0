using System;
using cashbook.Models.Enums;

namespace cashbook.Dto.exchangeRate;

public class UpsertExchangeRateDto
{
	/// <summary>معرّف الخزنة.</summary>
	public Guid BookId { get; set; }

	/// <summary>العملة: USD.</summary>
	public CurrencyCode Currency { get; set; } = CurrencyCode.USD;

	/// <summary>السعر بالدينار لكل وحدة، مثل 1480.</summary>
	public decimal Rate { get; set; }

	/// <summary>التاريخ (اليوم إن تُرك فارغاً).</summary>
	public DateTime? RateDate { get; set; }
}
