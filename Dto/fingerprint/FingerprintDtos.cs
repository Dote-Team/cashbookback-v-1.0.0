using System;
using System.Collections.Generic;

namespace cashbook.Dto.fingerprint;

/// <summary>صورة خام من البرنامج الوسيط: تدرّج رمادي 8 بت، Pixels بصيغة Base64 بطول Width × Height.</summary>
public class FingerprintSampleDto
{
	/// <summary>عرض الصورة بالبكسل، كما أعاده البرنامج الوسيط.</summary>
	public int Width { get; set; }

	/// <summary>ارتفاع الصورة بالبكسل.</summary>
	public int Height { get; set; }

	/// <summary>دقة الصورة (500 للقارئ ZK9500).</summary>
	public int Dpi { get; set; } = 500;

	/// <summary>الصورة الخام بصيغة Base64: بايت لكل بكسل (رمادي)، بطول Width × Height.</summary>
	public string Pixels { get; set; }
}

public class EnrollFingerprintDto
{
	/// <summary>المستخدم المراد تسجيل بصمته؛ فارغ = المستخدم الحالي.</summary>
	public Guid? UserId { get; set; }

	/// <summary>0-9: 0 إبهام اليمنى ... 4 خنصر اليمنى، 5 إبهام اليسرى ... 9 خنصر اليسرى.</summary>
	public int FingerIndex { get; set; }

	/// <summary>ثلاث لقطات للإصبع نفسه.</summary>
	public List<FingerprintSampleDto> Samples { get; set; } = new List<FingerprintSampleDto>();
}

public class FingerprintLoginDto
{
	/// <summary>اختياري: فارغ = البحث عن صاحب البصمة بين كل المستخدمين.</summary>
	public string? Username { get; set; }

	/// <summary>لقطة البصمة من البرنامج الوسيط.</summary>
	public FingerprintSampleDto Sample { get; set; }

	/// <summary>اختياري: معرّف الجهاز.</summary>
	public string? DeviceToken { get; set; }
}

public class FingerprintInfoDto
{
	public Guid Id { get; set; }

	public int FingerIndex { get; set; }

	public string Algorithm { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime? LastUsedAt { get; set; }
}

public class FingerprintStatusDto
{
	public bool Enabled { get; set; }

	public bool Available { get; set; }

	public string Matcher { get; set; }

	public int SamplesPerEnrollment { get; set; }

	public int MaxFingersPerUser { get; set; }

	public string? Reason { get; set; }
}
