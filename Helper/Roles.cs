namespace cashbook.Helper;

/// <summary>
/// قيم عمود BusinessUsers.Role بعد نظام الصلاحيات المتوارثة. العمود صار للتوافق والعرض فقط:
/// القرار في كل طلب يأتي من <see cref="Permissions"/> عبر IPermissionService.
/// </summary>
public static class Roles
{
	public const string SuperAdmin = "superadmin";

	public const string Admin = "admin";

	public const string User = "user";
}
