namespace cashbook.Helper;

/// <summary>أسماء سياسات تحديد معدل الطلبات المسجلة في Program.cs.</summary>
public static class RateLimitPolicies
{
	/// <summary>الدخول بكلمة المرور أو بالبصمة: 10 طلبات في الدقيقة لكل عنوان IP افتراضياً.</summary>
	public const string Login = "login";

	/// <summary>تجديد رمز الدخول: 30 طلباً في الدقيقة لكل عنوان IP افتراضياً.</summary>
	public const string Refresh = "refresh";
}
