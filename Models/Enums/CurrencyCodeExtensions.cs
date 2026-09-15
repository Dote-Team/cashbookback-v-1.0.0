using System;

namespace cashbook.Models.Enums;

public static class CurrencyCodeExtensions
{
	public const CurrencyCode Default = CurrencyCode.IQD;

	public static bool TryParse(string? value, out CurrencyCode currency)
	{
		currency = CurrencyCode.IQD;
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}
		string text = value.Trim().ToUpperInvariant();
		if (1 == 0)
		{
		}
		string text2;
		switch (text)
		{
		case "دولار":
		case "$":
		case "US":
		case "US DOLLAR":
		case "DOLLAR":
			text2 = "USD";
			break;
		case "دينار":
		case "IQ":
		case "IRAQI DINAR":
		case "DINAR":
			text2 = "IQD";
			break;
		default:
			text2 = text;
			break;
		}
		if (1 == 0)
		{
		}
		text = text2;
		return Enum.TryParse<CurrencyCode>(text, ignoreCase: true, out currency);
	}

	public static string Symbol(this CurrencyCode currency)
	{
		return (currency == CurrencyCode.USD) ? "$" : "د.ع";
	}
}
