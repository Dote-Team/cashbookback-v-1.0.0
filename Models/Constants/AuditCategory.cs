namespace cashbook.Models.Constants;

public static class AuditCategory
{
	public const string Auth = "Auth";

	public const string Data = "Data";

	public const string Access = "Access";

	public const string System = "System";

	public static readonly string[] All = new string[4] { "Auth", "Data", "Access", "System" };

	public static string Label(string? category)
	{
		if (1 == 0)
		{
		}
		string result = category switch
		{
			"Auth" => "مصادقة ودخول", 
			"Data" => "تغيير بيانات", 
			"Access" => "وصول وقراءة", 
			"System" => "أحداث النظام", 
			_ => "غير محد\u0651د", 
		};
		if (1 == 0)
		{
		}
		return result;
	}
}
