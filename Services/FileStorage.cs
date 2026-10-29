using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace cashbook.Services;

public enum UploadKind
{
	/// <summary>مرفقات القيود: صور أو PDF.</summary>
	Attachment,
	/// <summary>الصورة الشخصية: صور فقط.</summary>
	ProfileImage
}

public sealed class FileValidationException : Exception
{
	public FileValidationException(string message)
		: base(message)
	{
	}
}

public interface IFileStorage
{
	/// <summary>مجلد الرفع الوحيد في النظام.</summary>
	string Root { get; }

	/// <summary>يفحص الملفات قبل حفظ أي شيء. يعيد رسالة الخطأ، أو null إن كانت كلها مقبولة.</summary>
	string? Validate(IEnumerable<IFormFile>? files, UploadKind kind);

	/// <summary>يحفظ الملفات بأسماء عشوائية وامتداد يطابق محتواها الفعلي. يرمي FileValidationException عند الرفض.</summary>
	Task<List<string>> SaveAsync(IEnumerable<IFormFile> files, UploadKind kind);

	/// <summary>ينسخ ملفاً محفوظاً باسم جديد. يعيد null إن لم يوجد الأصل.</summary>
	string? Copy(string fileName);

	void Delete(IEnumerable<string> fileNames);

	/// <summary>المسار الكامل لملف داخل مجلد الرفع، أو null إن لم يوجد أو كان الاسم غير صالح.</summary>
	string? Resolve(string fileName);

	bool IsAllowedExtension(string fileName);

	/// <summary>
	/// اسم الملف مع توقيع مؤقت: name?exp=..&amp;sig=.. . الواجهة تضيفه إلى /API/Files/ كما هو،
	/// فيعمل داخل وسم img الذي لا يرسل رمز الدخول.
	/// </summary>
	[return: NotNullIfNotNull(nameof(fileName))]
	string? SignedName(string? fileName);

	bool VerifySignature(string fileName, long expires, string? signature);
}

/// <summary>
/// تخزين المرفقات في مكان واحد (ContentRoot/wwwroot/uploads). قبل هذا كان كل مستودع يبني
/// المسار بنفسه من Directory.GetCurrentDirectory، ولا فحص لنوع الملف ولا لحجمه.
/// </summary>
public sealed class FileStorage : IFileStorage
{
	private static readonly Dictionary<string, string[]> SignaturesByType = new Dictionary<string, string[]>
	{
		[".jpg"] = new[] { "FFD8FF" },
		[".png"] = new[] { "89504E470D0A1A0A" },
		[".gif"] = new[] { "474946383761", "474946383961" },
		[".webp"] = new[] { "52494646" },
		[".pdf"] = new[] { "25504446" }
	};

	private static readonly HashSet<string> AttachmentTypes = new HashSet<string> { ".jpg", ".png", ".gif", ".webp", ".pdf" };

	private static readonly HashSet<string> ImageTypes = new HashSet<string> { ".jpg", ".png", ".gif", ".webp" };

	private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf" };

	private readonly byte[] _signingKey;

	private readonly long _maxFileBytes;

	private readonly int _maxFilesPerRequest;

	private readonly TimeSpan _linkLifetime;

	public string Root { get; }

	public FileStorage(IWebHostEnvironment environment, IConfiguration configuration)
	{
		Root = Path.Combine(environment.ContentRootPath, "wwwroot", "uploads");
		Directory.CreateDirectory(Root);
		// مفتاح مشتق، لا المفتاح نفسه: توقيع رابط ملف لا يصلح أبداً كتوقيع رمز دخول.
		_signingKey = SHA256.HashData(Encoding.UTF8.GetBytes("cashbook-files:" + configuration.GetValue<string>("ApiSettings:Secret")));
		_maxFileBytes = Math.Max(1, configuration.GetValue<int?>("Uploads:MaxFileSizeMb") ?? 10) * 1024L * 1024L;
		_maxFilesPerRequest = Math.Max(1, configuration.GetValue<int?>("Uploads:MaxFilesPerRequest") ?? 5);
		_linkLifetime = TimeSpan.FromHours(Math.Max(1, configuration.GetValue<int?>("Uploads:LinkLifetimeHours") ?? 12));
	}

	public string? Validate(IEnumerable<IFormFile>? files, UploadKind kind)
	{
		List<IFormFile> list = files?.Where(f => f != null && f.Length > 0).ToList() ?? new List<IFormFile>();
		if (list.Count > _maxFilesPerRequest)
		{
			return $"الحد الأقصى {_maxFilesPerRequest} ملفات في المرة الواحدة.";
		}
		foreach (IFormFile file in list)
		{
			if (file.Length > _maxFileBytes)
			{
				return $"الملف «{Path.GetFileName(file.FileName)}» أكبر من {_maxFileBytes / 1024 / 1024} ميغابايت.";
			}
			string? type = DetectType(file);
			HashSet<string> allowed = kind == UploadKind.ProfileImage ? ImageTypes : AttachmentTypes;
			if (type == null || !allowed.Contains(type))
			{
				return kind == UploadKind.ProfileImage
					? $"الملف «{Path.GetFileName(file.FileName)}» ليس صورة. المسموح: JPG وPNG وGIF وWEBP."
					: $"نوع الملف «{Path.GetFileName(file.FileName)}» غير مسموح. المسموح: صور JPG وPNG وGIF وWEBP، وملفات PDF.";
			}
		}
		return null;
	}

	public async Task<List<string>> SaveAsync(IEnumerable<IFormFile> files, UploadKind kind)
	{
		List<IFormFile> list = files?.Where(f => f != null && f.Length > 0).ToList() ?? new List<IFormFile>();
		string? error = Validate(list, kind);
		if (error != null)
		{
			throw new FileValidationException(error);
		}
		List<string> saved = new List<string>();
		try
		{
			foreach (IFormFile file in list)
			{
				string name = Guid.NewGuid().ToString() + DetectType(file);
				using (FileStream stream = new FileStream(Path.Combine(Root, name), FileMode.CreateNew))
				{
					await file.CopyToAsync(stream);
				}
				saved.Add(name);
			}
			return saved;
		}
		catch
		{
			Delete(saved);
			throw;
		}
	}

	public string? Copy(string fileName)
	{
		string? source = Resolve(fileName);
		if (source == null)
		{
			return null;
		}
		string name = Guid.NewGuid().ToString() + Path.GetExtension(source).ToLowerInvariant();
		File.Copy(source, Path.Combine(Root, name));
		return name;
	}

	public void Delete(IEnumerable<string> fileNames)
	{
		foreach (string fileName in fileNames)
		{
			try
			{
				string? path = Resolve(fileName);
				if (path != null)
				{
					File.Delete(path);
				}
			}
			catch (IOException)
			{
			}
		}
	}

	public string? Resolve(string fileName)
	{
		// اسم الملف وحده، بلا أي مسار: يمنع الخروج من مجلد الرفع.
		string safeName = Path.GetFileName(fileName ?? string.Empty);
		if (string.IsNullOrEmpty(safeName) || safeName != fileName)
		{
			return null;
		}
		string path = Path.Combine(Root, safeName);
		return File.Exists(path) ? path : null;
	}

	public bool IsAllowedExtension(string fileName) => AllowedExtensions.Contains(Path.GetExtension(fileName ?? string.Empty));

	[return: NotNullIfNotNull(nameof(fileName))]
	public string? SignedName(string? fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
		{
			return fileName;
		}
		// انتهاء مقرّب إلى الساعة: الرابط نفسه يتكرر طوال الساعة، فيبقى المتصفح قادراً على تخزين الصورة.
		long hour = 3600;
		long expires = (DateTimeOffset.UtcNow.Add(_linkLifetime).ToUnixTimeSeconds() / hour + 1) * hour;
		return $"{fileName}?exp={expires}&sig={Sign(fileName, expires)}";
	}

	public bool VerifySignature(string fileName, long expires, string? signature)
	{
		if (string.IsNullOrEmpty(signature) || expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
		{
			return false;
		}
		return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(fileName, expires)), Encoding.ASCII.GetBytes(signature));
	}

	private string Sign(string fileName, long expires) =>
		WebEncoders.Base64UrlEncode(HMACSHA256.HashData(_signingKey, Encoding.UTF8.GetBytes(fileName + "|" + expires)));

	/// <summary>نوع الملف من بايتاته الأولى، لا من امتداده: ملف HTML باسم photo.jpg يُرفض.</summary>
	private static string? DetectType(IFormFile file)
	{
		byte[] header = new byte[12];
		int read;
		using (Stream stream = file.OpenReadStream())
		{
			read = stream.Read(header, 0, header.Length);
		}
		string hex = Convert.ToHexString(header, 0, read);
		foreach (KeyValuePair<string, string[]> pair in SignaturesByType)
		{
			if (pair.Value.Any(hex.StartsWith))
			{
				if (pair.Key == ".webp" && (read < 12 || Encoding.ASCII.GetString(header, 8, 4) != "WEBP"))
				{
					continue;
				}
				return pair.Key;
			}
		}
		return null;
	}
}
