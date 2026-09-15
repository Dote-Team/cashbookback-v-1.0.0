using System;
using cashbook.Models.Enums;

namespace cashbook.Validators;

public sealed class ResolvedTransactionValues
{
	public string? Type { get; init; }

	public decimal Amount { get; init; }

	public CurrencyCode Currency { get; init; }

	public decimal? ExchangeRate { get; init; }

	public DateTime? ExchangeDate { get; init; }

	public DateTime? Date { get; init; }
}
