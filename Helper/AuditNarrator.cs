using System;
using System.Collections.Generic;
using System.Globalization;
using cashbook.Models.Constants;

namespace cashbook.Helper;

public static class AuditNarrator
{
	private static readonly Dictionary<string, string> EntityLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["Transaction"] = "حركة",
		["TransactionHistory"] = "سجل حركة",
		["Book"] = "خزنة",
		["Business"] = "منشأة",
		["BusinessUser"] = "عضوية مستخدم",
		["User"] = "مستخدم",
		["Category"] = "تصنيف",
		["PaymentMethod"] = "طريقة دفع",
		["Contact"] = "جهة",
		["CustomField"] = "حقل مخصص",
		["CustomFieldValue"] = "قيمة حقل مخصص",
		["Attachement"] = "مرفق",
		["Attachment"] = "مرفق",
		["Setting"] = "إعداد",
		["ExchangeRate"] = "سعر صرف",
		["Session"] = "جلسة",
		["AuditLog"] = "سجل تدقيق"
	};

	private static readonly Dictionary<string, string> FieldLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["Amount"] = "المبلغ",
		["Type"] = "النوع",
		["Description"] = "الوصف",
		["Notes"] = "ملاحظات",
		["TDate"] = "التاريخ",
		["ExchangeRate"] = "سعر الصرف",
		["ExchangeDate"] = "تاريخ الصرف",
		["Currency"] = "العملة",
		["Operation"] = "العملية",
		["Name"] = "الاسم",
		["Title"] = "العنوان",
		["BookId"] = "الخزنة",
		["BusinessId"] = "المنشأة",
		["Balance"] = "الرصيد",
		["OpeningBalance"] = "الرصيد الافتتاحي",
		["IsActive"] = "م\u064fفع\u064e\u0651ل",
		["IsDeleted"] = "محذوف",
		["Username"] = "اسم المستخدم",
		["Email"] = "البريد الإلكتروني",
		["Password"] = "كلمة المرور",
		["Role"] = "الدور",
		["Roles"] = "الأدوار",
		["BookIds"] = "الخزائن المسندة",
		["IsSuperAdmin"] = "مدير رئيسي",
		["ProfileImage"] = "الصورة الشخصية",
		["Phone"] = "الهاتف",
		["Address"] = "العنوان",
		["CategoryId"] = "التصنيف",
		["ContactId"] = "الجهة",
		["PaymentMethodId"] = "طريقة الدفع",
		["CustomFieldId"] = "الحقل المخصص",
		["Value"] = "القيمة",
		["FieldType"] = "نوع الحقل",
		["Key"] = "المفتاح",
		["CreatedAt"] = "تاريخ الإنشاء",
		["UpdatedAt"] = "تاريخ التعديل",
		["UserId"] = "المستخدم",
		["DeviceToken"] = "بصمة الجهاز",
		["RefreshToken"] = "رمز التجديد",
		["ExpiredAt"] = "تاريخ الانتهاء",
		["TransactionId"] = "الحركة",
		["FileName"] = "اسم الملف"
	};

	public static string LabelEntity(string? entityName)
	{
		if (string.IsNullOrWhiteSpace(entityName))
		{
			return "كيان";
		}
		string value;
		return EntityLabels.TryGetValue(entityName, out value) ? value : entityName;
	}

	public static string LabelField(string? fieldName)
	{
		if (string.IsNullOrWhiteSpace(fieldName))
		{
			return "حقل";
		}
		string value;
		return FieldLabels.TryGetValue(fieldName, out value) ? value : fieldName;
	}

	public static string DescribeAction(string? action)
	{
		if (string.IsNullOrWhiteSpace(action))
		{
			return "عملية";
		}
		string text = AuditAction.Label(action);
		if (text != action)
		{
			return text;
		}
		int num = action.IndexOf('.');
		if (num <= 0 || num == action.Length - 1)
		{
			return action;
		}
		string text2 = action.Substring(0, num);
		string text3 = action.Substring(num + 1);
		string text4 = (EntityLabels.TryGetValue(text2, out string value) ? value : text2);
		if (1 == 0)
		{
		}
		string text5 = text3 switch
		{
			"create" => "إضافة", 
			"update" => "تعديل", 
			"delete" => "حذف", 
			"read" => "قراءة", 
			"change" => "تغيير", 
			"duplicate" => "نسخ", 
			_ => text3, 
		};
		if (1 == 0)
		{
		}
		string text6 = text5;
		return text6 + " " + text4;
	}

	public static string FormatValue(string? value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "—";
		}
		if (Guid.TryParse(value, out var result))
		{
			return result.ToString("N").Substring(0, 8) + "…";
		}
		if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result2))
		{
			return result2.ToString("#,##0.###", CultureInfo.InvariantCulture);
		}
		if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result3))
		{
			return result3.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
		}
		if (bool.TryParse(value, out var result4))
		{
			return result4 ? "نعم" : "لا";
		}
		return (value.Length > 80) ? (value.Substring(0, 80) + "…") : value;
	}

	public static (string Action, string Category, string Severity) Classify(string method, string path, int statusCode, string? requestBody)
	{
		bool flag = statusCode >= 400;
		string text = path.ToLowerInvariant();
		string text2 = MethodVerb(method);
		string text3 = "Info";
		string item;
		string item2;
		if (text.Contains("/user/login"))
		{
			item = (flag ? "user.login.failed" : "user.login.success");
			item2 = "Auth";
			if (flag)
			{
				text3 = "Warning";
			}
		}
		else if (text.Contains("/user/register"))
		{
			item = "user.register";
			item2 = "Auth";
		}
		else if (text.Contains("/user/refresh-token"))
		{
			item = "user.token.refresh";
			item2 = "Auth";
		}
		else if (text.Contains("/user/logout"))
		{
			item = "user.logout";
			item2 = "Auth";
		}
		else if (text2 == "delete" && text.Contains("/user"))
		{
			item = "user.delete";
			item2 = "Auth";
			text3 = "Critical";
		}
		else if (text2 == "update" && text.Contains("/user") && MentionsPassword(requestBody))
		{
			item = "user.password.change";
			item2 = "Auth";
			text3 = "Critical";
		}
		else if (text2 == "update" && text.Contains("/user"))
		{
			item = "user.profile.update";
			item2 = "Auth";
			text3 = "Warning";
		}
		else if (text.Contains("/invitation"))
		{
			item = ((text2 == "delete") ? "invitation.revoke" : "invitation.create");
			item2 = "Auth";
			text3 = (MentionsOwnerRole(requestBody) ? "Critical" : "Warning");
		}
		else if (text.Contains("/exchangerate"))
		{
			item = "exchangerate.change";
			item2 = "System";
			text3 = "Warning";
		}
		else if (text.Contains("/backup"))
		{
			item = (text.Contains("restore") ? "backup.restore" : "backup.create");
			item2 = "System";
			text3 = "Critical";
		}
		else
		{
			item = AuditAction.Generic(ExtractResource(path), text2);
			item2 = "Access";
		}
		if (statusCode >= 500)
		{
			text3 = AuditSeverity.Max(text3, "Critical");
		}
		else if (flag)
		{
			text3 = AuditSeverity.Max(text3, "Warning");
		}
		return (Action: item, Category: item2, Severity: text3);
	}

	private static string MethodVerb(string method)
	{
		string text = method.ToUpperInvariant();
		if (1 == 0)
		{
		}
		string result;
		switch (text)
		{
		case "POST":
			result = "create";
			break;
		case "PUT":
		case "PATCH":
			result = "update";
			break;
		case "DELETE":
			result = "delete";
			break;
		default:
			result = "read";
			break;
		}
		if (1 == 0)
		{
		}
		return result;
	}

	private static string ExtractResource(string path)
	{
		string[] array = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
		foreach (string text in array)
		{
			if (!text.Equals("API", StringComparison.OrdinalIgnoreCase))
			{
				return text.ToLowerInvariant();
			}
		}
		return "unknown";
	}

	private static bool MentionsPassword(string? body)
	{
		return !string.IsNullOrEmpty(body) && body.Contains("\"password\"", StringComparison.OrdinalIgnoreCase);
	}

	private static bool MentionsOwnerRole(string? body)
	{
		return !string.IsNullOrEmpty(body) && body.Contains("\"owner\"", StringComparison.OrdinalIgnoreCase);
	}
}
