namespace cashbook.Models;

public class MailSettings
{
	/// <summary>
	/// مفتاح تشغيل الإرسال. عند التعطيل يُتخطّى الإرسال بلا أي محاولة اتصال،
	/// فتنجح إضافة العضو فوراً بلا انتظار خادم بريد غير مُعدّ.
	/// </summary>
	public bool Enabled { get; set; }

	public string FromName { get; set; }

	public string FromEmail { get; set; }

	public string SmtpServer { get; set; }

	public int SmtpPort { get; set; }

	/// <summary>اسم مستخدم SMTP عند الحاجة إلى مصادقة. يُترك فارغاً للخوادم الداخلية بلا مصادقة.</summary>
	public string Email { get; set; }

	/// <summary>
	/// كلمة مرور SMTP. لا تُكتب في appsettings.json أبداً — تُقرأ من .env
	/// أو من متغيّرات البيئة (MailSettings__Password).
	/// </summary>
	public string Password { get; set; }

	/// <summary>عند <c>true</c>: STARTTLS مع المنفذ 587، وSSL مباشر مع المنفذ 465.</summary>
	public bool UseSsl { get; set; }

	/// <summary>مهلة الاتصال والإرسال بالثواني — تمنع تعليق الطلب على خادم لا يستجيب.</summary>
	public int TimeoutSeconds { get; set; } = 15;
}
