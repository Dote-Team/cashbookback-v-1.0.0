using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace cashbook.Helper;

/// <summary>
/// رموز الصلاحيات. كل صلاحية تُمنح لمستخدم على خزنة بعينها (UserWalletPermissions)،
/// والفحص الحقيقي يجري في الخادم دائماً عبر <see cref="cashbook.Services.IPermissionService"/>.
///
/// قاعدة الوراثة: لا يمنح أحد صلاحية لا يملكها هو على الخزنة نفسها،
/// و<see cref="UsersManage"/> لا يمنحها إلا السوبر أدمن، فلا يتجاوز النظام ثلاثة مستويات.
/// </summary>
public static class Permissions
{
	public const string BookView = "book.view";
	public const string BookEdit = "book.edit";
	public const string BookArchive = "book.archive";

	public const string TransactionView = "transaction.view";
	public const string TransactionCreate = "transaction.create";
	public const string TransactionEdit = "transaction.edit";
	public const string TransactionArchive = "transaction.archive";
	public const string TransactionDuplicate = "transaction.duplicate";
	public const string TransactionExport = "transaction.export";

	public const string HistoryView = "history.view";

	public const string CategoryManage = "category.manage";
	public const string ContactManage = "contact.manage";
	public const string PaymentMethodManage = "payment_method.manage";
	public const string CustomFieldManage = "custom_field.manage";

	public const string ExchangeRateManage = "exchange_rate.manage";
	public const string BackupImport = "backup.import";
	public const string ArchiveView = "archive.view";

	public const string UsersManage = "users.manage";

	public sealed record Definition(string Code, string Name, string Group, bool SuperAdminGrantOnly);

	public static readonly IReadOnlyList<Definition> Catalog = new List<Definition>
	{
		new(BookView, "عرض الخزنة ورصيدها", "الخزنة", false),
		new(BookEdit, "تعديل اسم الخزنة وإعداداتها", "الخزنة", false),
		new(BookArchive, "أرشفة الخزنة", "الخزنة", false),
		new(TransactionView, "عرض القيود", "القيود", false),
		new(TransactionCreate, "إضافة قيد", "القيود", false),
		new(TransactionEdit, "تعديل قيد", "القيود", false),
		new(TransactionArchive, "أرشفة قيد", "القيود", false),
		new(TransactionDuplicate, "نسخ قيد إلى خزنة أخرى", "القيود", false),
		new(TransactionExport, "تصدير وطباعة ونسخ احتياطي", "القيود", false),
		new(HistoryView, "عرض سجل تعديلات القيود", "القيود", false),
		new(CategoryManage, "إدارة التصنيفات", "البيانات المرجعية", false),
		new(ContactManage, "إدارة جهات الاتصال", "البيانات المرجعية", false),
		new(PaymentMethodManage, "إدارة طرق الدفع", "البيانات المرجعية", false),
		new(CustomFieldManage, "إدارة الحقول المخصصة", "البيانات المرجعية", false),
		new(ExchangeRateManage, "إدارة سعر الصرف", "أخرى", false),
		new(BackupImport, "استيراد نسخة احتياطية", "أخرى", false),
		new(ArchiveView, "عرض الأرشيف", "أخرى", false),
		new(UsersManage, "إدارة المستخدمين", "أخرى", true),
	};

	public static readonly IReadOnlySet<string> All = Catalog.Select(d => d.Code).ToHashSet();

	public static bool IsKnown([NotNullWhen(true)] string? code) => code != null && All.Contains(code);

	public static bool IsSuperAdminGrantOnly(string code) =>
		Catalog.Any(d => d.Code == code && d.SuperAdminGrantOnly);
}
