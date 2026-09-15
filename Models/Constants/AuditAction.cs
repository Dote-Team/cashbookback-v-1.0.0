namespace cashbook.Models.Constants;

public static class AuditAction
{
	public const string LoginSuccess = "user.login.success";

	public const string LoginFailed = "user.login.failed";

	public const string Register = "user.register";

	public const string Logout = "user.logout";

	public const string RefreshToken = "user.token.refresh";

	public const string PasswordChange = "user.password.change";

	public const string ProfileUpdate = "user.profile.update";

	public const string UserCreate = "user.create";

	public const string UserDelete = "user.delete";

	public const string InvitationCreate = "invitation.create";

	public const string InvitationRevoke = "invitation.revoke";

	public const string MembershipChange = "membership.change";

	public const string TransactionCreate = "transaction.create";

	public const string TransactionUpdate = "transaction.update";

	public const string TransactionDelete = "transaction.delete";

	public const string TransactionDuplicate = "transaction.duplicate";

	public const string BookCreate = "book.create";

	public const string BookUpdate = "book.update";

	public const string BookDelete = "book.delete";

	public const string BusinessCreate = "business.create";

	public const string BusinessUpdate = "business.update";

	public const string BusinessDelete = "business.delete";

	public const string ContactChange = "contact.change";

	public const string CategoryChange = "category.change";

	public const string PaymentMethodChange = "paymentmethod.change";

	public const string CustomFieldChange = "customfield.change";

	public const string SettingChange = "setting.change";

	public const string ExchangeRateChange = "exchangerate.change";

	public const string BackupCreate = "backup.create";

	public const string BackupRestore = "backup.restore";

	public static string Generic(string resource, string verb)
	{
		return resource + "." + verb;
	}

	public static string Label(string? action)
	{
		if (1 == 0)
		{
		}
		string result = action switch
		{
			"user.login.success" => "دخول ناجح", 
			"user.login.failed" => "محاولة دخول فاشلة", 
			"user.register" => "تسجيل حساب جديد", 
			"user.logout" => "خروج", 
			"user.token.refresh" => "تجديد رمز دخول", 
			"user.password.change" => "تغيير كلمة مرور", 
			"user.profile.update" => "تعديل بيانات الحساب", 
			"user.create" => "إنشاء مستخدم", 
			"user.delete" => "حذف مستخدم", 
			"invitation.create" => "إرسال دعوة", 
			"invitation.revoke" => "إلغاء دعوة", 
			"transaction.create" => "إضافة حركة", 
			"transaction.update" => "تعديل حركة", 
			"transaction.delete" => "حذف حركة", 
			"transaction.duplicate" => "نسخ حركة", 
			"business.create" => "إنشاء منشأة", 
			"business.update" => "تعديل منشأة", 
			"business.delete" => "حذف منشأة", 
			"book.create" => "إنشاء خزنة", 
			"book.update" => "تعديل خزنة", 
			"book.delete" => "حذف خزنة", 
			"exchangerate.change" => "تعديل سعر صرف", 
			"backup.create" => "إنشاء نسخة احتياطية", 
			"backup.restore" => "استعادة نسخة احتياطية", 
			_ => action ?? "عملية غير معروفة", 
		};
		if (1 == 0)
		{
		}
		return result;
	}
}
