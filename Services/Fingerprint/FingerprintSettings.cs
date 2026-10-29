namespace cashbook.Services.Fingerprint;

/// <summary>إعدادات قسم Fingerprint في appsettings.json، والمفتاح في .env.</summary>
public class FingerprintSettings
{
	public bool Enabled { get; set; } = true;

	/// <summary>sourceafis (الإنتاج) أو dev (التطوير فقط، مع البرنامج الوسيط في وضع المحاكاة).</summary>
	public string Matcher { get; set; } = "sourceafis";

	/// <summary>
	/// حد قبول التطابق. 40 هو الحد الموصى به في SourceAFIS. يُعاير بعد وصول الجهاز:
	/// رفعه يقلل قبول بصمة خاطئة ويزيد رفض صاحب البصمة.
	/// </summary>
	public double MatchThreshold { get; set; } = 40;

	public int SamplesPerEnrollment { get; set; } = 3;

	public int MaxFingersPerUser { get; set; } = 3;

	/// <summary>مفتاح AES-256 بصيغة Base64 (32 بايت) لتشفير القوالب. يوضع في .env: Fingerprint__EncryptionKey</summary>
	public string EncryptionKey { get; set; } = "";

	public int MaxImageDimension { get; set; } = 1200;
}
