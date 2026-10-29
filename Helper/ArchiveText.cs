namespace cashbook.Helper;

public static class ArchiveText
{
	public const int MaxReasonLength = 500;

	/// <summary>سبب الأرشفة اختياري؛ يُنظّف ويُقصّ إلى طول العمود.</summary>
	public static string? Reason(string? reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
		{
			return null;
		}
		string trimmed = reason.Trim();
		return trimmed.Length <= MaxReasonLength ? trimmed : trimmed.Substring(0, MaxReasonLength);
	}
}
