namespace cashbook.Models.Constants;

public static class TransactionTypes
{
	public const string CashIn = "cash in";

	public const string CashOut = "cash out";

	public static string? Normalize(string? type)
	{
		if (string.IsNullOrWhiteSpace(type))
		{
			return null;
		}
		string text = type.Trim().ToLowerInvariant().Replace('_', ' ')
			.Replace('-', ' ');
		while (text.Contains("  "))
		{
			text = text.Replace("  ", " ");
		}
		if (1 == 0)
		{
		}
		string result;
		switch (text)
		{
		case "cash in":
		case "cashin":
		case "in":
		case "deposit":
		case "income":
		case "credit":
		case "إيداع":
		case "ايداع":
		case "إدخال":
		case "ادخال":
			result = "cash in";
			break;
		case "cash out":
		case "cashout":
		case "out":
		case "withdraw":
		case "withdrawal":
		case "expense":
		case "debit":
		case "سحب":
		case "إخراج":
		case "اخراج":
			result = "cash out";
			break;
		default:
			result = null;
			break;
		}
		if (1 == 0)
		{
		}
		return result;
	}

	public static bool IsCashIn(string? type)
	{
		return Normalize(type) == "cash in";
	}

	public static bool IsCashOut(string? type)
	{
		return Normalize(type) == "cash out";
	}

	public static bool IsValid(string? type)
	{
		return Normalize(type) != null;
	}

	public static int Direction(string? type)
	{
		string text = Normalize(type);
		if (1 == 0)
		{
		}
		int result = ((text == "cash in") ? 1 : ((text == "cash out") ? (-1) : 0));
		if (1 == 0)
		{
		}
		return result;
	}
}
