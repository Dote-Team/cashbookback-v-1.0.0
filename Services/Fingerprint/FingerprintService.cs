using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using cashbook.Data;
using cashbook.Dto.fingerprint;
using cashbook.Models;

namespace cashbook.Services.Fingerprint;

public interface IFingerprintService
{
	FingerprintStatusDto Status();

	Task<ServiceResult> EnrollAsync(Guid callerId, EnrollFingerprintDto dto);

	Task<ServiceResult> RevokeAsync(Guid callerId, Guid fingerprintId);

	Task<ServiceResult> ListAsync(Guid callerId, Guid userId);

	/// <summary>
	/// يعيد صاحب البصمة عند التطابق، وإلا null. مع اسم المستخدم: مطابقة 1:1 على بصماته فقط.
	/// بدونه: بحث 1:N في بصمات كل المستخدمين الفعّالين، ويُرفض إن طابقت أكثر من مستخدم.
	/// </summary>
	Task<User?> VerifyAsync(string? username, FingerprintSampleDto sample);
}

/// <summary>
/// تسجيل البصمات والتحقق منها. القوالب تُشفَّر بـ AES-256-GCM قبل الحفظ ولا تُعاد في أي
/// استجابة. البصمة لا تُحذف: الإلغاء يجعلها غير فعّالة.
/// </summary>
public sealed class FingerprintService : IFingerprintService
{
	private readonly ApplicationDbContext _context;

	private readonly IFingerprintMatcher _matcher;

	private readonly FingerprintSettings _settings;

	private readonly IUserManagementService _users;

	private readonly ILogger<FingerprintService> _logger;

	private readonly byte[]? _key;

	private readonly string? _unavailableReason;

	public FingerprintService(ApplicationDbContext context, IFingerprintMatcher matcher, IOptions<FingerprintSettings> settings, IUserManagementService users, IHostEnvironment environment, ILogger<FingerprintService> logger)
	{
		_context = context;
		_matcher = matcher;
		_settings = settings.Value;
		_users = users;
		_logger = logger;
		_key = TryReadKey(_settings.EncryptionKey);
		if (!_settings.Enabled)
		{
			_unavailableReason = "الدخول بالبصمة معطّل في الإعدادات.";
		}
		else if (_key == null)
		{
			_unavailableReason = "مفتاح تشفير البصمات (Fingerprint__EncryptionKey) غير مضبوط في .env.";
		}
		else if (matcher is DevelopmentMatcher && !environment.IsDevelopment())
		{
			_unavailableReason = "المحرّك التجريبي dev لا يعمل خارج بيئة التطوير.";
		}
	}

	private bool Available => _unavailableReason == null;

	public FingerprintStatusDto Status() => new FingerprintStatusDto
	{
		Enabled = _settings.Enabled,
		Available = Available,
		Matcher = _matcher.Algorithm,
		SamplesPerEnrollment = _settings.SamplesPerEnrollment,
		MaxFingersPerUser = _settings.MaxFingersPerUser,
		Reason = _unavailableReason
	};

	public async Task<ServiceResult> EnrollAsync(Guid callerId, EnrollFingerprintDto dto)
	{
		if (!Available)
		{
			return ServiceResult.Fail(HttpStatusCode.ServiceUnavailable, _unavailableReason!);
		}
		Guid targetId = dto.UserId ?? callerId;
		if (targetId != callerId && !await _users.CanManageAsync(callerId, targetId))
		{
			return ServiceResult.Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية تسجيل بصمة لهذا المستخدم.");
		}
		User? user = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetId);
		if (user == null || !user.IsActive)
		{
			return ServiceResult.Fail(HttpStatusCode.NotFound, "المستخدم غير موجود أو معطّل.");
		}
		if (dto.FingerIndex < 0 || dto.FingerIndex > 9)
		{
			return ServiceResult.Fail(HttpStatusCode.BadRequest, "رقم الإصبع يجب أن يكون بين 0 و 9.");
		}
		if (dto.Samples == null || dto.Samples.Count != _settings.SamplesPerEnrollment)
		{
			return ServiceResult.Fail(HttpStatusCode.BadRequest, $"التسجيل يحتاج {_settings.SamplesPerEnrollment} لمسات للإصبع نفسه.");
		}
		List<byte[]> templates = new List<byte[]>();
		foreach (FingerprintSampleDto sampleDto in dto.Samples)
		{
			(FingerprintSample? sample, string? error) = Parse(sampleDto);
			if (sample == null)
			{
				return ServiceResult.Fail(HttpStatusCode.BadRequest, error!);
			}
			templates.Add(_matcher.Extract(sample));
		}
		// اللمسات الثلاث يجب أن تتطابق فيما بينها، وإلا فالتسجيل رديء أو لأصابع مختلفة.
		for (int i = 0; i < templates.Count; i++)
		{
			for (int j = i + 1; j < templates.Count; j++)
			{
				if (_matcher.Score(templates[i], templates[j]) < _settings.MatchThreshold)
				{
					return ServiceResult.Fail(HttpStatusCode.UnprocessableEntity, "اللمسات غير متطابقة. أعد التسجيل بالإصبع نفسه وضعه في منتصف القارئ.");
				}
			}
		}
		List<UserFingerprint> active = await _context.UserFingerprints.Where(f => f.UserId == targetId && f.IsActive).ToListAsync();
		UserFingerprint? sameFinger = active.FirstOrDefault(f => f.FingerIndex == dto.FingerIndex);
		if (sameFinger == null && active.Count >= _settings.MaxFingersPerUser)
		{
			return ServiceResult.Fail(HttpStatusCode.Conflict, $"الحد الأقصى {_settings.MaxFingersPerUser} أصابع لكل مستخدم. ألغِ إصبعاً أولاً.");
		}
		// إعادة تسجيل الإصبع نفسه تلغي القديم ولا تحذفه.
		if (sameFinger != null)
		{
			sameFinger.IsActive = false;
			sameFinger.RevokedAt = DateTime.UtcNow;
			sameFinger.RevokedByUserId = callerId;
		}
		UserFingerprint fingerprint = new UserFingerprint
		{
			UserId = targetId,
			FingerIndex = dto.FingerIndex,
			Algorithm = _matcher.Algorithm,
			TemplateEncrypted = Encrypt(JsonSerializer.SerializeToUtf8Bytes(templates.Select(Convert.ToBase64String).ToList())),
			CreatedByUserId = callerId
		};
		_context.UserFingerprints.Add(fingerprint);
		await _context.SaveChangesAsync();
		return ServiceResult.Success(new { fingerprint.Id, fingerprint.FingerIndex });
	}

	public async Task<ServiceResult> RevokeAsync(Guid callerId, Guid fingerprintId)
	{
		UserFingerprint? fingerprint = await _context.UserFingerprints.FirstOrDefaultAsync(f => f.Id == fingerprintId && f.IsActive);
		if (fingerprint == null)
		{
			return ServiceResult.Fail(HttpStatusCode.NotFound, "البصمة غير موجودة.");
		}
		if (fingerprint.UserId != callerId && !await _users.CanManageAsync(callerId, fingerprint.UserId))
		{
			return ServiceResult.Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية إلغاء هذه البصمة.");
		}
		fingerprint.IsActive = false;
		fingerprint.RevokedAt = DateTime.UtcNow;
		fingerprint.RevokedByUserId = callerId;
		await _context.SaveChangesAsync();
		return ServiceResult.Success();
	}

	public async Task<ServiceResult> ListAsync(Guid callerId, Guid userId)
	{
		if (userId != callerId && !await _users.CanManageAsync(callerId, userId))
		{
			return ServiceResult.Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية عرض بصمات هذا المستخدم.");
		}
		List<FingerprintInfoDto> items = await _context.UserFingerprints.AsNoTracking()
			.Where(f => f.UserId == userId && f.IsActive)
			.OrderBy(f => f.FingerIndex)
			.Select(f => new FingerprintInfoDto
			{
				Id = f.Id,
				FingerIndex = f.FingerIndex,
				Algorithm = f.Algorithm,
				CreatedAt = f.CreatedAt,
				LastUsedAt = f.LastUsedAt
			}).ToListAsync();
		return ServiceResult.Success(items);
	}

	public async Task<User?> VerifyAsync(string? username, FingerprintSampleDto sampleDto)
	{
		if (!Available)
		{
			return null;
		}
		(FingerprintSample? sample, string? _) = Parse(sampleDto);
		if (sample == null)
		{
			return null;
		}
		IQueryable<UserFingerprint> query = _context.UserFingerprints
			.Where(f => f.IsActive && f.Algorithm == _matcher.Algorithm && f.User.IsActive);
		if (!string.IsNullOrWhiteSpace(username))
		{
			string identifier = username.Trim();
			User? owner = await _context.Users.FirstOrDefaultAsync(u => u.Username == identifier || u.Email == identifier);
			if (owner == null || !owner.IsActive)
			{
				return null;
			}
			query = query.Where(f => f.UserId == owner.Id);
		}
		List<UserFingerprint> fingerprints = await query.ToListAsync();
		if (fingerprints.Count == 0)
		{
			return null;
		}
		byte[] probe = _matcher.Extract(sample);
		// أعلى درجة لكل مستخدم: في البحث 1:N تطابق مستخدمَين فوق الحد يعني التباساً لا دخولاً.
		Dictionary<Guid, (UserFingerprint Fingerprint, double Score)> bestPerUser = new Dictionary<Guid, (UserFingerprint, double)>();
		foreach (UserFingerprint fingerprint in fingerprints)
		{
			List<string>? stored;
			try
			{
				stored = JsonSerializer.Deserialize<List<string>>(Decrypt(fingerprint.TemplateEncrypted));
			}
			catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
			{
				_logger.LogError(ex, "تعذّر فك تشفير بصمة {FingerprintId}: هل تغيّر مفتاح التشفير؟", fingerprint.Id);
				continue;
			}
			foreach (string template in stored ?? new List<string>())
			{
				double score = _matcher.Score(Convert.FromBase64String(template), probe);
				if (score >= _settings.MatchThreshold && (!bestPerUser.TryGetValue(fingerprint.UserId, out var current) || score > current.Score))
				{
					bestPerUser[fingerprint.UserId] = (fingerprint, score);
				}
			}
		}
		if (bestPerUser.Count != 1)
		{
			if (bestPerUser.Count > 1)
			{
				_logger.LogWarning("بصمة طابقت {Count} مستخدمين؛ رُفض الدخول. ارفع Fingerprint__MatchThreshold أو اطلب اسم المستخدم.", bestPerUser.Count);
			}
			return null;
		}
		UserFingerprint matched = bestPerUser.Values.Single().Fingerprint;
		matched.LastUsedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();
		return await _context.Users.FirstAsync(u => u.Id == matched.UserId);
	}

	private (FingerprintSample? Sample, string? Error) Parse(FingerprintSampleDto? dto)
	{
		if (dto == null || string.IsNullOrEmpty(dto.Pixels))
		{
			return (null, "صورة البصمة مفقودة.");
		}
		if (dto.Width < 50 || dto.Height < 50 || dto.Width > _settings.MaxImageDimension || dto.Height > _settings.MaxImageDimension)
		{
			return (null, "أبعاد صورة البصمة غير صالحة.");
		}
		if (dto.Dpi < 200 || dto.Dpi > 1500)
		{
			return (null, "دقة صورة البصمة (DPI) غير صالحة.");
		}
		byte[] pixels;
		try
		{
			pixels = Convert.FromBase64String(dto.Pixels);
		}
		catch (FormatException)
		{
			return (null, "صورة البصمة ليست Base64 صالحاً.");
		}
		if (pixels.Length != dto.Width * dto.Height)
		{
			return (null, "حجم صورة البصمة لا يطابق أبعادها.");
		}
		return (new FingerprintSample(dto.Width, dto.Height, dto.Dpi, pixels), null);
	}

	private byte[] Encrypt(byte[] plain)
	{
		byte[] nonce = RandomNumberGenerator.GetBytes(12);
		byte[] tag = new byte[16];
		byte[] cipher = new byte[plain.Length];
		using AesGcm aes = new AesGcm(_key!, 16);
		aes.Encrypt(nonce, plain, cipher, tag);
		return nonce.Concat(tag).Concat(cipher).ToArray();
	}

	private byte[] Decrypt(byte[] blob)
	{
		byte[] nonce = blob[..12];
		byte[] tag = blob[12..28];
		byte[] cipher = blob[28..];
		byte[] plain = new byte[cipher.Length];
		using AesGcm aes = new AesGcm(_key!, 16);
		aes.Decrypt(nonce, cipher, tag, plain);
		return plain;
	}

	private static byte[]? TryReadKey(string? base64)
	{
		if (string.IsNullOrWhiteSpace(base64))
		{
			return null;
		}
		try
		{
			byte[] key = Convert.FromBase64String(base64.Trim());
			return key.Length == 32 ? key : null;
		}
		catch (FormatException)
		{
			return null;
		}
	}
}
