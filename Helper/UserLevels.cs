namespace cashbook.Helper;

/// <summary>
/// مستويات المستخدمين الثلاثة. المستوى الأعلى ينشئ المستوى الذي تحته فقط:
/// السوبر أدمن ينشئ الأدمن، والأدمن ينشئ المستخدم، والمستخدم لا ينشئ أحداً.
/// </summary>
public static class UserLevels
{
	public const int SuperAdmin = 1;
	public const int Admin = 2;
	public const int User = 3;

	public static string Name(int level) => level switch
	{
		SuperAdmin => "superadmin",
		Admin => "admin",
		_ => "user",
	};

	public static string ArabicName(int level) => level switch
	{
		SuperAdmin => "سوبر أدمن",
		Admin => "أدمن",
		_ => "مستخدم",
	};
}
