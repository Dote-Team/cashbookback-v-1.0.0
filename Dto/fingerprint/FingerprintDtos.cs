using System;
using System.Collections.Generic;

namespace cashbook.Dto.fingerprint;

/// <summary>صورة خام من البرنامج الوسيط: تدرّج رمادي 8 بت، Pixels بصيغة Base64 بطول Width × Height.</summary>
public class FingerprintSampleDto
{
	public int Width { get; set; }

	public int Height { get; set; }

	public int Dpi { get; set; } = 500;

	public string Pixels { get; set; }
}

public class EnrollFingerprintDto
{
	/// <summary>المستخدم المراد تسجيل بصمته؛ فارغ = المستخدم الحالي.</summary>
	public Guid? UserId { get; set; }

	/// <summary>0-9: 0 إبهام اليمنى ... 4 خنصر اليمنى، 5 إبهام اليسرى ... 9 خنصر اليسرى.</summary>
	public int FingerIndex { get; set; }

	public List<FingerprintSampleDto> Samples { get; set; } = new List<FingerprintSampleDto>();
}

public class FingerprintLoginDto
{
	/// <summary>اختياري: فارغ = البحث عن صاحب البصمة بين كل المستخدمين.</summary>
	public string? Username { get; set; }

	public FingerprintSampleDto Sample { get; set; }

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
