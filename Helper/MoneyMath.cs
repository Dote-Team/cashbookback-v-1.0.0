using System;
using cashbook.Models.Enums;

namespace cashbook.Helper;

public static class MoneyMath
{
	public const int UsdDecimals = 2;

	public const int IqdDecimals = 3;

	public const int RateDecimals = 6;

	public static int DecimalsFor(CurrencyCode currency)
	{
		if (1 == 0)
		{
		}
		int result = ((currency != CurrencyCode.USD) ? 3 : 2);
		if (1 == 0)
		{
		}
		return result;
	}

	public static decimal Round(decimal amount, CurrencyCode currency)
	{
		return Math.Round(amount, DecimalsFor(currency), MidpointRounding.AwayFromZero);
	}

	public static decimal RoundRate(decimal rate)
	{
		return Math.Round(rate, 6, MidpointRounding.AwayFromZero);
	}

	public static bool IsValidScale(decimal amount, CurrencyCode currency)
	{
		return amount == Round(amount, currency);
	}

	public static bool TryValidateAmount(decimal amount, CurrencyCode currency, out string? error)
	{
		error = null;
		if (amount <= 0m)
		{
			error = "المبلغ يجب أن يكون أكبر من صفر.";
			return false;
		}
		if (!IsValidScale(amount, currency))
		{
			error = ((currency == CurrencyCode.USD) ? $"مبلغ الدولار يقبل مرتبتين عشريتين فقط (مثال: 10.12) — القيمة الم\u064fرسلة {amount} غير صحيحة." : $"مبلغ الدينار العراقي يقبل ثلاث مراتب عشرية كحد أقصى (مثال: 1000.123) — القيمة الم\u064fرسلة {amount} غير صحيحة.");
			return false;
		}
		return true;
	}

	public static bool TryValidateRate(decimal? rate, out string? error)
	{
		error = null;
		if (!rate.HasValue || rate.Value <= 0m)
		{
			error = "سعر الصرف إلزامي ويجب أن يكون أكبر من صفر.";
			return false;
		}
		if (rate.Value != RoundRate(rate.Value))
		{
			error = $"سعر الصرف يقبل {6} مراتب عشرية كحد أقصى.";
			return false;
		}
		return true;
	}
}
