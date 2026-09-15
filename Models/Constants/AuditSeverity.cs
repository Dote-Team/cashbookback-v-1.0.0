namespace cashbook.Models.Constants;

public static class AuditSeverity
{
	public const string Info = "Info";

	public const string Warning = "Warning";

	public const string Critical = "Critical";

	public static int Rank(string? severity)
	{
		if (1 == 0)
		{
		}
		int result = ((severity == "Critical") ? 3 : ((!(severity == "Warning")) ? 1 : 2));
		if (1 == 0)
		{
		}
		return result;
	}

	public static string Label(string? severity)
	{
		if (1 == 0)
		{
		}
		string result = ((severity == "Critical") ? "خطر" : ((!(severity == "Warning")) ? "عادي" : "تحذير"));
		if (1 == 0)
		{
		}
		return result;
	}

	public static string Max(string? a, string? b)
	{
		return (Rank(a) >= Rank(b)) ? (a ?? "Info") : (b ?? "Info");
	}
}
