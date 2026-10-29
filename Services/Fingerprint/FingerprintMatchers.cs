using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using SourceAFIS;

namespace cashbook.Services.Fingerprint;

/// <summary>صورة بصمة خام كما يلتقطها القارئ: تدرّج رمادي 8 بت، بايت لكل بكسل، صفاً بعد صف.</summary>
public sealed record FingerprintSample(int Width, int Height, int Dpi, byte[] Pixels);

/// <summary>
/// محرّك المطابقة في الخادم. المطابقة تتم هنا دائماً، لا في جهاز المستخدم:
/// لو طابق جهاز المستخدم بنفسه لأمكن تزوير النتيجة.
/// </summary>
public interface IFingerprintMatcher
{
	/// <summary>يُخزَّن مع القالب، فلا يُطابَق قالب من محرّك بآخر.</summary>
	string Algorithm { get; }

	/// <summary>يحوّل صورة إلى قالب قابل للتخزين.</summary>
	byte[] Extract(FingerprintSample sample);

	/// <summary>درجة التطابق بين قالبين. القيمة الأعلى = تطابق أقوى.</summary>
	double Score(byte[] enrolledTemplate, byte[] probeTemplate);
}

/// <summary>
/// SourceAFIS: مكتبة مطابقة مفتوحة المصدر تعمل على صورة البصمة نفسها، فلا تحتاج ZKFinger SDK
/// على الخادم (يعمل في Docker على Linux). حد التطابق الموصى به 40 (خطأ قبول ~0.01%).
/// </summary>
public sealed class SourceAfisMatcher : IFingerprintMatcher
{
	public string Algorithm => "sourceafis-3";

	public byte[] Extract(FingerprintSample sample)
	{
		FingerprintImage image = new FingerprintImage(sample.Width, sample.Height, sample.Pixels, new FingerprintImageOptions { Dpi = sample.Dpi });
		return new FingerprintTemplate(image).ToByteArray();
	}

	public double Score(byte[] enrolledTemplate, byte[] probeTemplate)
	{
		FingerprintMatcher matcher = new FingerprintMatcher(new FingerprintTemplate(probeTemplate));
		return matcher.Match(new FingerprintTemplate(enrolledTemplate));
	}
}

/// <summary>
/// محرّك تجريبي للتطوير فقط: يطابق الصور المتطابقة بايتاً ببايت. يتيح تجربة الدورة كاملة
/// (تسجيل ثم دخول) مع البرنامج الوسيط في وضع المحاكاة قبل توفر الجهاز. يُرفض تفعيله في الإنتاج.
/// </summary>
public sealed class DevelopmentMatcher : IFingerprintMatcher
{
	public string Algorithm => "dev-sha256";

	public byte[] Extract(FingerprintSample sample) => SHA256.HashData(sample.Pixels);

	public double Score(byte[] enrolledTemplate, byte[] probeTemplate) =>
		CryptographicOperations.FixedTimeEquals(enrolledTemplate, probeTemplate) ? 100 : 0;
}
