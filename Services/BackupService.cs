using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using cashbook.Data;
using cashbook.Dto.backup;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Models.Constants;
using cashbook.Models.Enums;

namespace cashbook.Services;

public class BackupService : IBackupService
{
	private static readonly byte[] Magic = Encoding.ASCII.GetBytes("CBK1");

	private const byte CurrentFormatVersion = 1;

	private const int SaltSize = 16;

	private const int NonceSize = 12;

	private const int TagSize = 16;

	private const int HeaderSize = 49;

	private const int KeySize = 32;

	private const string ManifestEntryName = "manifest.json";

	private const string TransactionsEntryName = "transactions.json";

	private const string AttachmentsFolder = "attachments/";

	private static readonly JsonSerializerOptions JsonWriteOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private static readonly JsonSerializerOptions JsonReadOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly ApplicationDbContext _context;

	private readonly int _pbkdf2Iterations;

	private static string UploadRoot => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

	public BackupService(ApplicationDbContext context, IConfiguration configuration)
	{
		_context = context;
		_pbkdf2Iterations = configuration.GetValue<int?>("Backup:Pbkdf2Iterations") ?? 200000;
	}

	public async Task<byte[]> ExportBookAsync(Guid bookId, string password, Guid userId)
	{
		ValidatePassword(password);
		Book book = (await _context.Books.Include((Book b) => b.Business).FirstOrDefaultAsync((Book b) => b.Id == bookId)) ?? throw new InvalidOperationException("الخزنة غير موجودة.");
		List<Transaction> transactions = await (from t in _context.Transactions.Where((Transaction t) => t.BookId == bookId).Include((Transaction t) => t.Category).Include((Transaction t) => t.PaymentMethod)
				.Include((Transaction t) => t.Contact)
				.Include((Transaction t) => t.CustomFieldValues)
				.ThenInclude((CustomFieldValue v) => v.CustomField)
				.Include((Transaction t) => t.Attachements)
			orderby t.Date
			select t).ToListAsync();
		List<CustomField> customFields = await (from cf in _context.CustomFields
			where cf.BookId == bookId
			orderby cf.Key
			select cf).ToListAsync();
		string exportedBy = await (from u in _context.Users
			where u.Id == userId
			select u.Username ?? u.Email).FirstOrDefaultAsync();
		BackupManifestDto manifest = new BackupManifestDto
		{
			FormatVersion = 1,
			SourceBookId = book.Id,
			SourceBookName = book.Name,
			SourceBusinessName = book.Business?.Name,
			ExportedAtUtc = DateTime.UtcNow,
			ExportedBy = exportedBy,
			SourceSystem = "CashBook",
			TransactionCount = transactions.Count,
			AttachmentCount = transactions.SelectMany((Transaction t) => t.Attachements ?? new List<Attachement>()).Count(),
			DateFrom = (transactions.Any() ? new DateTime?(transactions.Min((Transaction t) => t.Date)) : ((DateTime?)null)),
			DateTo = (transactions.Any() ? new DateTime?(transactions.Max((Transaction t) => t.Date)) : ((DateTime?)null)),
			Currencies = (from c in transactions.Select((Transaction t) => t.Currency.ToString()).Distinct()
				orderby c
				select c).ToList(),
			Categories = (from n in (from t in transactions
					where t.Category != null
					select t.Category.Name).Distinct()
				orderby n
				select n).ToList(),
			PaymentMethods = (from n in (from t in transactions
					where t.PaymentMethod != null
					select t.PaymentMethod.Name).Distinct()
				orderby n
				select n).ToList(),
			Contacts = (from n in (from t in transactions
					where t.Contact != null
					select t.Contact.Name).Distinct()
				orderby n
				select n).ToList(),
			CustomFields = customFields.Select((CustomField cf) => new BackupCustomFieldDto
			{
				Key = cf.Key,
				IsRequired = cf.IsRequired
			}).ToList()
		};
		List<BackupTransactionDto> transactionDtos = transactions.Select((Transaction t) => new BackupTransactionDto
		{
			Type = t.Type,
			Date = t.Date,
			Description = t.Description,
			Amount = t.Amount,
			Currency = t.Currency.ToString(),
			ExchangeRate = t.ExchangeRate,
			ExchangeDate = t.ExchangeDate,
			CategoryName = t.Category?.Name,
			PaymentMethodName = t.PaymentMethod?.Name,
			ContactName = t.Contact?.Name,
			ContactPhone = t.Contact?.Phone,
			CreatedAt = t.CreatedAt,
			CustomFieldValues = (from v in t.CustomFieldValues ?? new List<CustomFieldValue>()
				where v.CustomField != null
				select new BackupCustomFieldValueDto
				{
					Key = v.CustomField.Key,
					Value = v.Value
				}).ToList(),
			Attachments = (from a in t.Attachements ?? new List<Attachement>()
				where !string.IsNullOrWhiteSpace(a.Files)
				select a.Files).ToList()
		}).ToList();
		byte[] archiveBytes;
		using (MemoryStream archiveStream = new MemoryStream())
		{
			using (ZipArchive archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
			{
				WriteJsonEntry(archive, "manifest.json", manifest);
				WriteJsonEntry(archive, "transactions.json", transactionDtos);
				HashSet<string> addedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (string fileName in transactionDtos.SelectMany((BackupTransactionDto t) => t.Attachments))
				{
					if (!addedFiles.Add(fileName))
					{
						continue;
					}
					string fullPath = Path.Combine(UploadRoot, fileName);
					if (!File.Exists(fullPath))
					{
						continue;
					}
					ZipArchiveEntry entry = archive.CreateEntry("attachments/" + fileName, CompressionLevel.Optimal);
					using Stream entryStream = entry.Open();
					using FileStream sourceStream = File.OpenRead(fullPath);
					await sourceStream.CopyToAsync(entryStream);
				}
			}
			archiveBytes = archiveStream.ToArray();
		}
		return Encrypt(archiveBytes, password);
	}

	public Task<BackupManifestDto> InspectAsync(Stream file, string password)
	{
		ValidatePassword(password);
		byte[] plain = Decrypt(ReadAllBytes(file), password);
		using ZipArchive archive = OpenArchive(plain);
		BackupManifestDto result = ReadJsonEntry<BackupManifestDto>(archive, "manifest.json") ?? throw new InvalidOperationException("الملف لا يحتوي على بيانات وصفية صالحة.");
		return Task.FromResult(result);
	}

	public async Task<BackupImportResultDto> ImportAsync(Stream file, string password, Guid businessId, Guid? targetBookId, string? newBookName, Guid userId)
	{
		ValidatePassword(password);
		byte[] plain = Decrypt(ReadAllBytes(file), password);
		using ZipArchive archive = OpenArchive(plain);
		BackupManifestDto manifest = ReadJsonEntry<BackupManifestDto>(archive, "manifest.json") ?? throw new InvalidOperationException("الملف لا يحتوي على بيانات وصفية صالحة.");
		if (manifest.FormatVersion > 1)
		{
			throw new InvalidOperationException("إصدار الملف أحدث من الإصدار الذي يدعمه النظام.");
		}
		List<BackupTransactionDto> transactions = ReadJsonEntry<List<BackupTransactionDto>>(archive, "transactions.json") ?? new List<BackupTransactionDto>();
		BackupImportResultDto result = new BackupImportResultDto();
		Book targetBook;
		if (targetBookId.HasValue && targetBookId.Value != Guid.Empty)
		{
			targetBook = (await _context.Books.FirstOrDefaultAsync((Book b) => b.Id == ((Guid?)targetBookId).Value && b.BusinessId == businessId)) ?? throw new InvalidOperationException("الخزنة الهدف غير موجودة أو لا تنتمي لهذه المنشأة.");
			result.CreatedNewBook = false;
		}
		else
		{
			string desiredName = ((!string.IsNullOrWhiteSpace(newBookName)) ? newBookName.Trim() : manifest.SourceBookName);
			if (string.IsNullOrWhiteSpace(desiredName))
			{
				desiredName = "Imported Box";
			}
			Book book = new Book();
			Book book2 = book;
			book2.Name = await BuildUniqueBookNameAsync(businessId, desiredName);
			book.BusinessId = businessId;
			book.CreatedAt = DateTime.Now;
			book.UpdatedAt = DateTime.Now;
			targetBook = book;
			_context.Books.Add(targetBook);
			_context.Settings.Add(new Setting
			{
				BookId = targetBook.Id
			});
			await _context.SaveChangesAsync();
			result.CreatedNewBook = true;
		}
		result.TargetBookId = targetBook.Id;
		result.TargetBookName = targetBook.Name;
		Dictionary<string, Category> categories = await _context.Categories.Where((Category c) => c.BusinessId == businessId).ToDictionaryAsync((Category c) => c.Name.ToLower(), (Category c) => c);
		Dictionary<string, PaymentMethod> paymentMethods = await _context.PaymentMethods.Where((PaymentMethod p) => p.BusinessId == businessId).ToDictionaryAsync((PaymentMethod p) => p.Name.ToLower(), (PaymentMethod p) => p);
		Dictionary<string, Contact> contacts = await _context.Contacts.Where((Contact c) => c.BusinessId == businessId).ToDictionaryAsync((Contact c) => c.Name.ToLower(), (Contact c) => c);
		Dictionary<string, CustomField> customFields = await _context.CustomFields.Where((CustomField cf) => cf.BookId == targetBook.Id).ToDictionaryAsync((CustomField cf) => cf.Key.ToLower(), (CustomField cf) => cf);
		Directory.CreateDirectory(UploadRoot);
		using IDbContextTransaction dbTransaction = await _context.Database.BeginTransactionAsync();
		try
		{
			foreach (BackupTransactionDto dto in transactions)
			{
				CurrencyCode currency = (CurrencyCodeExtensions.TryParse(dto.Currency, out var parsed) ? parsed : CurrencyCode.IQD);
				string normalizedType = TransactionTypes.Normalize(dto.Type) ?? dto.Type;
				Transaction transaction = new Transaction
				{
					Type = normalizedType,
					Date = dto.Date,
					Description = dto.Description,
					Amount = dto.Amount,
					Currency = currency,
					ExchangeRate = dto.ExchangeRate,
					ExchangeDate = dto.ExchangeDate,
					BookId = targetBook.Id,
					UserId = userId,
					CreatedAt = DateTime.Now,
					UpdatedAt = DateTime.Now
				};
				if (!string.IsNullOrWhiteSpace(dto.CategoryName))
				{
					string key = dto.CategoryName.Trim().ToLower();
					if (!categories.TryGetValue(key, out var category))
					{
						category = new Category
						{
							Name = dto.CategoryName.Trim(),
							BusinessId = businessId
						};
						_context.Categories.Add(category);
						categories[key] = category;
						result.CategoriesCreated++;
					}
					transaction.Category = category;
					category = null;
				}
				if (!string.IsNullOrWhiteSpace(dto.PaymentMethodName))
				{
					string key2 = dto.PaymentMethodName.Trim().ToLower();
					if (!paymentMethods.TryGetValue(key2, out var paymentMethod))
					{
						paymentMethod = new PaymentMethod
						{
							Name = dto.PaymentMethodName.Trim(),
							BusinessId = businessId
						};
						_context.PaymentMethods.Add(paymentMethod);
						paymentMethods[key2] = paymentMethod;
						result.PaymentMethodsCreated++;
					}
					transaction.PaymentMethod = paymentMethod;
					paymentMethod = null;
				}
				if (!string.IsNullOrWhiteSpace(dto.ContactName))
				{
					string key3 = dto.ContactName.Trim().ToLower();
					if (!contacts.TryGetValue(key3, out var contact))
					{
						contact = new Contact
						{
							Name = dto.ContactName.Trim(),
							Phone = (dto.ContactPhone ?? string.Empty),
							BusinessId = businessId
						};
						_context.Contacts.Add(contact);
						contacts[key3] = contact;
						result.ContactsCreated++;
					}
					transaction.Contact = contact;
					contact = null;
				}
				_context.Transactions.Add(transaction);
				await _context.SaveChangesAsync();
				foreach (BackupCustomFieldValueDto valueDto in dto.CustomFieldValues ?? new List<BackupCustomFieldValueDto>())
				{
					if (!string.IsNullOrWhiteSpace(valueDto.Key))
					{
						string key4 = valueDto.Key.Trim().ToLower();
						if (!customFields.TryGetValue(key4, out var customField))
						{
							customField = new CustomField
							{
								Key = valueDto.Key.Trim(),
								BookId = targetBook.Id,
								IsRequired = false
							};
							_context.CustomFields.Add(customField);
							await _context.SaveChangesAsync();
							customFields[key4] = customField;
							result.CustomFieldsCreated++;
						}
						_context.CustomFieldValues.Add(new CustomFieldValue
						{
							Value = (valueDto.Value ?? string.Empty),
							CustomFieldId = customField.Id,
							TransactionId = transaction.Id
						});
						customField = null;
					}
				}
				foreach (string attachmentName in dto.Attachments ?? new List<string>())
				{
					ZipArchiveEntry entry = archive.GetEntry("attachments/" + attachmentName);
					if (entry == null)
					{
						continue;
					}
					string newFileName = string.Concat(str1: Path.GetExtension(attachmentName), str0: Guid.NewGuid().ToString());
					string destination = Path.Combine(UploadRoot, newFileName);
					using (Stream entryStream = entry.Open())
					{
						using FileStream destinationStream = File.Create(destination);
						await entryStream.CopyToAsync(destinationStream);
					}
					_context.Attachements.Add(new Attachement
					{
						Files = newFileName,
						TransactionId = transaction.Id
					});
					result.AttachmentsImported++;
				}
				_context.TransactionHistories.Add(new TransactionHistory
				{
					Operation = "IMPORT",
					Description = "استيراد من نسخة احتياطية (" + manifest.SourceBookName + ")",
					Amount = transaction.Amount,
					Type = transaction.Type,
					ExchangeRate = transaction.ExchangeRate,
					ExchangeDate = transaction.ExchangeDate,
					From = null,
					To = null,
					TransactionId = transaction.Id,
					BookId = targetBook.Id,
					UserId = userId
				});
				result.TransactionsImported++;
			}
			await _context.SaveChangesAsync();
			await dbTransaction.CommitAsync();
		}
		catch
		{
			await dbTransaction.RollbackAsync();
			throw;
		}
		return result;
	}

	public async Task DumpAsync(string filePath, string password, string? outputDirectory = null)
	{
		if (!File.Exists(filePath))
		{
			throw new FileNotFoundException("ملف النسخة الاحتياطية غير موجود.", filePath);
		}
		byte[] data = await File.ReadAllBytesAsync(filePath);
		byte[] plain = Decrypt(data, password);
		using ZipArchive archive = OpenArchive(plain);
		Console.WriteLine($"حجم الملف المشف\u0651ر : {data.Length} بايت");
		Console.WriteLine($"حجم الأرشيف بعد الفك : {plain.Length} بايت");
		Console.WriteLine($"\nمحتوى الأرشيف ({archive.Entries.Count} مدخل):");
		foreach (ZipArchiveEntry entry in archive.Entries)
		{
			Console.WriteLine($"    {entry.FullName,-45} {entry.Length,10} بايت");
		}
		BackupManifestDto manifest = ReadJsonEntry<BackupManifestDto>(archive, "manifest.json");
		List<BackupTransactionDto> transactions = ReadJsonEntry<List<BackupTransactionDto>>(archive, "transactions.json") ?? new List<BackupTransactionDto>();
		string target = outputDirectory ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? ".", Path.GetFileNameWithoutExtension(filePath) + "-decrypted");
		Directory.CreateDirectory(target);
		if (manifest != null)
		{
			await File.WriteAllTextAsync(Path.Combine(target, "manifest.json"), JsonSerializer.Serialize(manifest, JsonWriteOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}
		await File.WriteAllTextAsync(Path.Combine(target, "transactions.json"), JsonSerializer.Serialize(transactions, JsonWriteOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		foreach (ZipArchiveEntry entry2 in archive.Entries.Where((ZipArchiveEntry e) => e.FullName.StartsWith("attachments/", StringComparison.Ordinal)))
		{
			string destination = Path.Combine(target, entry2.FullName.Replace('/', Path.DirectorySeparatorChar));
			Directory.CreateDirectory(Path.GetDirectoryName(destination));
			using Stream entryStream = entry2.Open();
			using FileStream destinationStream = File.Create(destination);
			await entryStream.CopyToAsync(destinationStream);
		}
		Console.WriteLine("\n================ البيان (manifest.json) ================");
		Console.WriteLine(JsonSerializer.Serialize(manifest, JsonWriteOptions));
		if (transactions.Count > 0)
		{
			Console.WriteLine($"\n================ أول حركتين من {transactions.Count} ================");
			Console.WriteLine(JsonSerializer.Serialize(transactions.Take(2), JsonWriteOptions));
		}
		Console.WriteLine("\nتم فك التشفير إلى المجلد:\n    " + target);
	}

	private static void ValidatePassword(string password)
	{
		if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
		{
			throw new InvalidOperationException("كلمة مرور النسخة الاحتياطية إلزامية ويجب ألا تقل عن 6 أحرف.");
		}
	}

	private static byte[] ReadAllBytes(Stream stream)
	{
		if (stream is MemoryStream memoryStream)
		{
			return memoryStream.ToArray();
		}
		using MemoryStream memoryStream2 = new MemoryStream();
		stream.CopyTo(memoryStream2);
		return memoryStream2.ToArray();
	}

	private static ZipArchive OpenArchive(byte[] plain)
	{
		try
		{
			return new ZipArchive(new MemoryStream(plain), ZipArchiveMode.Read);
		}
		catch (InvalidDataException)
		{
			throw new InvalidOperationException("محتوى الملف تالف ولا يمكن قراءته.");
		}
	}

	private static void WriteJsonEntry<T>(ZipArchive archive, string entryName, T value)
	{
		ZipArchiveEntry zipArchiveEntry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
		using Stream stream = zipArchiveEntry.Open();
		using StreamWriter streamWriter = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		streamWriter.Write(JsonSerializer.Serialize(value, JsonWriteOptions));
	}

	private static T? ReadJsonEntry<T>(ZipArchive archive, string entryName)
	{
		ZipArchiveEntry entry = archive.GetEntry(entryName);
		if (entry == null)
		{
			return default(T);
		}
		using Stream stream = entry.Open();
		using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
		return JsonSerializer.Deserialize<T>(streamReader.ReadToEnd(), JsonReadOptions);
	}

	private async Task<string> BuildUniqueBookNameAsync(Guid businessId, string desiredName)
	{
		HashSet<string> taken = new HashSet<string>(await (from b in _context.Books
			where b.BusinessId == businessId
			select b.Name).ToListAsync(), StringComparer.OrdinalIgnoreCase);
		if (!taken.Contains(desiredName))
		{
			return desiredName;
		}
		int index = 2;
		while (taken.Contains($"{desiredName} ({index})"))
		{
			index++;
		}
		return $"{desiredName} ({index})";
	}

	private byte[] Encrypt(byte[] plain, string password)
	{
		byte[] bytes = RandomNumberGenerator.GetBytes(16);
		byte[] bytes2 = RandomNumberGenerator.GetBytes(12);
		byte[] key = DeriveKey(password, bytes);
		byte[] array = new byte[plain.Length];
		byte[] array2 = new byte[16];
		using (AesGcm aesGcm = new AesGcm(key, 16))
		{
			aesGcm.Encrypt(bytes2, plain, array, array2);
		}
		using MemoryStream memoryStream = new MemoryStream();
		memoryStream.Write(Magic);
		memoryStream.WriteByte(1);
		memoryStream.Write(bytes);
		memoryStream.Write(bytes2);
		memoryStream.Write(array2);
		memoryStream.Write(array);
		return memoryStream.ToArray();
	}

	private byte[] Decrypt(byte[] data, string password)
	{
		if (data.Length <= 49)
		{
			throw new InvalidOperationException("الملف غير صالح أو تالف (الحجم أصغر من ترويسة النسخة الاحتياطية).");
		}
		if (!data.AsSpan(0, Magic.Length).SequenceEqual(Magic))
		{
			throw new InvalidOperationException("الملف ليس ملف نسخة احتياطية صادرا\u064b من هذا النظام.");
		}
		byte b = data[4];
		if (b > 1)
		{
			throw new InvalidOperationException("إصدار الملف أحدث من الإصدار الذي يدعمه النظام.");
		}
		byte[] salt = data.AsSpan(5, 16).ToArray();
		byte[] nonce = data.AsSpan(21, 12).ToArray();
		byte[] tag = data.AsSpan(33, 16).ToArray();
		byte[] array = data.AsSpan(49).ToArray();
		byte[] key = DeriveKey(password, salt);
		byte[] array2 = new byte[array.Length];
		try
		{
			using AesGcm aesGcm = new AesGcm(key, 16);
			aesGcm.Decrypt(nonce, array, tag, array2);
		}
		catch (CryptographicException)
		{
			throw new InvalidOperationException("كلمة المرور غير صحيحة أو أن الملف تعر\u0651ض للتلف أو التعديل.");
		}
		return array2;
	}

	private byte[] DeriveKey(string password, byte[] salt)
	{
		return Rfc2898DeriveBytes.Pbkdf2(password, salt, _pbkdf2Iterations, HashAlgorithmName.SHA256, 32);
	}
}
