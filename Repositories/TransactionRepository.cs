using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.Book;
using cashbook.Dto.category;
using cashbook.Dto.contact;
using cashbook.Dto.paymentMethod;
using cashbook.Dto.transaction;
using cashbook.Dto.user;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Models.Constants;
using cashbook.Models.Enums;
using cashbook.Validators;

namespace cashbook.Repositories;

public class TransactionRepository : Repository<Transaction>, ITransactionRepository, IRepository<Transaction>
{
	private sealed class RunningRow
	{
		public Guid Id { get; set; }

		public decimal RunIqd { get; set; }

		public decimal RunUsd { get; set; }
	}

	private readonly ApplicationDbContext _context;

	public TransactionRepository(ApplicationDbContext context, IConfiguration configuration)
		: base(context)
	{
		_context = context;
	}

	public async Task<bool> CreateTransactionAsync(CreateTransactionDto dto, Guid userId)
	{
		try
		{
			List<CustomFieldValueCreateDto> customFieldValues = new List<CustomFieldValueCreateDto>();
			if (!string.IsNullOrWhiteSpace(dto.CustomFieldValues))
			{
				customFieldValues = JsonSerializer.Deserialize<List<CustomFieldValueCreateDto>>(dto.CustomFieldValues, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});
			}
			string normalizedType = TransactionTypes.Normalize(dto.Type) ?? dto.Type;
			bool isUsd = dto.Currency == CurrencyCode.USD;
			decimal? exchangeRate = (isUsd ? dto.ExchangeRate : ((decimal?)null));
			DateTime? exchangeDate = ((isUsd && normalizedType != "cash out") ? dto.ExchangeDate : ((DateTime?)null));
			Transaction transaction = new Transaction
			{
				Type = normalizedType,
				Date = dto.Date,
				Description = ((dto.Description != null) ? dto.Description : null),
				Amount = dto.Amount,
				Currency = dto.Currency,
				ExchangeRate = exchangeRate,
				ExchangeDate = exchangeDate,
				CategoryId = dto.CategoryId,
				BookId = dto.BookId,
				ContactId = dto.ContactId,
				PaymentMethodId = dto.PaymentMethodId,
				UserId = userId
			};
			_context.Transactions.Add(transaction);
			await _context.SaveChangesAsync();
			if (customFieldValues?.Any() ?? false)
			{
				IEnumerable<CustomFieldValue> entities = customFieldValues.Select((CustomFieldValueCreateDto cfvDto) => new CustomFieldValue
				{
					Value = cfvDto.Value,
					CustomFieldId = cfvDto.CustomFieldId,
					TransactionId = transaction.Id
				});
				await _context.CustomFieldValues.AddRangeAsync(entities);
				await _context.SaveChangesAsync();
			}
			if (dto.Files != null && dto.Files.Any())
			{
				foreach (string fileName in await UploadFilesAsync(uploadPath: Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads"), files: dto.Files))
				{
					Attachement attachment = new Attachement
					{
						Files = fileName,
						TransactionId = transaction.Id
					};
					_context.Attachements.Add(attachment);
				}
				await _context.SaveChangesAsync();
			}
			TransactionHistory transactionHistory = new TransactionHistory
			{
				Operation = "POST",
				Description = ((dto.Description != null) ? dto.Description : null),
				Amount = dto.Amount,
				Type = transaction.Type,
				ExchangeRate = transaction.ExchangeRate,
				ExchangeDate = transaction.ExchangeDate,
				From = null,
				To = null,
				TransactionId = transaction.Id,
				BookId = dto.BookId,
				UserId = userId
			};
			_context.TransactionHistories.Add(transactionHistory);
			await _context.SaveChangesAsync();
			return true;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			throw new InvalidOperationException("تعذ\u0651ر حفظ الحركة. تحقق من البيانات وحاول مرة أخرى.", ex2);
		}
	}

	public async Task<bool> DeleteTransactionAsync(Guid transactionId)
	{
		Transaction transaction = await _context.Transactions.Include((Transaction t) => t.CustomFieldValues).Include((Transaction t) => t.Attachements).FirstOrDefaultAsync((Transaction t) => t.Id == transactionId);
		if (transaction == null)
		{
			return false;
		}
		TransactionHistory transactionHistory = new TransactionHistory
		{
			Operation = "DELETE",
			Description = transaction.Description,
			Amount = transaction.Amount,
			Type = transaction.Type,
			ExchangeRate = transaction.ExchangeRate,
			ExchangeDate = transaction.ExchangeDate,
			From = null,
			To = null,
			TransactionId = null,
			BookId = transaction.BookId,
			UserId = transaction.UserId,
			CreatedAt = DateTime.UtcNow
		};
		_context.TransactionHistories.Add(transactionHistory);
		string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
		if (transaction.Attachements != null && transaction.Attachements.Any())
		{
			foreach (Attachement attachment in transaction.Attachements)
			{
				if (string.IsNullOrEmpty(attachment.Files))
				{
					continue;
				}
				string filePath = Path.Combine(uploadsFolder, attachment.Files);
				if (File.Exists(filePath))
				{
					try
					{
						File.Delete(filePath);
					}
					catch (Exception ex)
					{
						Exception ex2 = ex;
						Console.WriteLine("Failed to delete file " + attachment.Files + ": " + ex2.Message);
					}
				}
			}
			_context.Attachements.RemoveRange(transaction.Attachements);
		}
		if (transaction.CustomFieldValues != null && transaction.CustomFieldValues.Any())
		{
			_context.CustomFieldValues.RemoveRange(transaction.CustomFieldValues);
		}
		_context.Transactions.Remove(transaction);
		await _context.SaveChangesAsync();
		return true;
	}

	public async Task<PaginatedResponse<TransactionDto>> GetAllTransactionsByBookIdAsync(Guid bookId, int? skip = 1, int? take = 25, decimal? amount = null, string searchCategory = null, string searchContact = null, string searchPaymentMethod = null, string searchType = null, string searchUser = null, DateTime? startDate = null, DateTime? endDate = null, SortField? sortBy = null, SortDirection? sortDirection = SortDirection.asc)
	{
		int currentPage = Math.Max(1, skip ?? 1);
		int currentSize = take ?? 25;
		if (currentSize <= 0)
		{
			return new PaginatedResponse<TransactionDto>
			{
				TotalRecords = 0,
				Skip = currentPage,
				Take = currentSize,
				Data = new List<TransactionDto>()
			};
		}
		List<object> parameters = new List<object>();
		List<string> filters = new List<string> { "t.BookId = " + Param(bookId) };
		if (amount.HasValue)
		{
			filters.Add("t.Amount = " + Param(amount.Value));
		}
		if (!string.IsNullOrEmpty(searchCategory))
		{
			filters.Add("CHARINDEX(" + Param(searchCategory) + ", c.Name) > 0");
		}
		if (!string.IsNullOrEmpty(searchContact))
		{
			filters.Add("CHARINDEX(" + Param(searchContact) + ", co.Name) > 0");
		}
		if (!string.IsNullOrEmpty(searchPaymentMethod))
		{
			filters.Add("CHARINDEX(" + Param(searchPaymentMethod) + ", pm.Name) > 0");
		}
		if (!string.IsNullOrEmpty(searchUser))
		{
			string p = Param(searchUser);
			filters.Add($"(CHARINDEX({p}, u.Name) > 0 OR CHARINDEX({p}, u.Email) > 0)");
		}
		if (!string.IsNullOrEmpty(searchType))
		{
			filters.Add("t.Type = " + Param(TransactionTypes.Normalize(searchType) ?? searchType.Trim().ToLowerInvariant()));
		}
		if (startDate.HasValue)
		{
			filters.Add("t.Date >= " + Param(startDate.Value));
		}
		if (endDate.HasValue)
		{
			filters.Add("t.Date <= " + Param(endDate.Value));
		}
		string whereSql = "WHERE " + string.Join("\n  AND ", filters);
		int totalRecords = await ExecuteScalarIntAsync("SELECT COUNT(*) FROM Transactions t \nLEFT JOIN Categories     c  ON c.Id  = t.CategoryId\nLEFT JOIN Contacts       co ON co.Id = t.ContactId\nLEFT JOIN PaymentMethods pm ON pm.Id = t.PaymentMethodId\nLEFT JOIN Users          u  ON u.Id  = t.UserId\n" + whereSql, parameters);
		if (1 == 0)
		{
		}
		string text;
		switch (sortBy)
		{
		case SortField.Category:
			if (sortDirection.HasValue)
			{
				SortDirection valueOrDefault = sortDirection.GetValueOrDefault();
				SortDirection sortDirection2 = valueOrDefault;
				if (sortDirection2 == SortDirection.asc)
				{
					text = "c.Name ASC, t.Id ASC";
					break;
				}
				if (sortDirection2 == SortDirection.desc)
				{
					text = "c.Name DESC, t.Id ASC";
					break;
				}
			}
			goto default;
		case SortField.PaymentMethod:
			if (sortDirection.HasValue)
			{
				SortDirection valueOrDefault = sortDirection.GetValueOrDefault();
				SortDirection sortDirection2 = valueOrDefault;
				if (sortDirection2 == SortDirection.asc)
				{
					text = "pm.Name ASC, t.Id ASC";
					break;
				}
				if (sortDirection2 == SortDirection.desc)
				{
					text = "pm.Name DESC, t.Id ASC";
					break;
				}
			}
			goto default;
		case SortField.Description:
			if (sortDirection.HasValue)
			{
				SortDirection valueOrDefault = sortDirection.GetValueOrDefault();
				SortDirection sortDirection2 = valueOrDefault;
				if (sortDirection2 == SortDirection.asc)
				{
					text = "t.Description ASC, t.Id ASC";
					break;
				}
				if (sortDirection2 == SortDirection.desc)
				{
					text = "t.Description DESC, t.Id ASC";
					break;
				}
			}
			goto default;
		case SortField.Amount:
			if (sortDirection.HasValue)
			{
				SortDirection valueOrDefault = sortDirection.GetValueOrDefault();
				SortDirection sortDirection2 = valueOrDefault;
				if (sortDirection2 == SortDirection.asc)
				{
					text = "t.Amount ASC, t.Id ASC";
					break;
				}
				if (sortDirection2 == SortDirection.desc)
				{
					text = "t.Amount DESC, t.Id ASC";
					break;
				}
			}
			goto default;
		case SortField.NewBalance:
			if (sortDirection.HasValue)
			{
				SortDirection valueOrDefault = sortDirection.GetValueOrDefault();
				SortDirection sortDirection2 = valueOrDefault;
				if (sortDirection2 == SortDirection.asc)
				{
					text = "CASE WHEN t.Currency = N'USD' THEN r.RunUsd ELSE r.RunIqd END ASC, t.Id ASC";
					break;
				}
				if (sortDirection2 == SortDirection.desc)
				{
					text = "CASE WHEN t.Currency = N'USD' THEN r.RunUsd ELSE r.RunIqd END DESC, t.Id ASC";
					break;
				}
			}
			goto default;
		case SortField.Date:
			if (sortDirection.HasValue)
			{
				SortDirection valueOrDefault = sortDirection.GetValueOrDefault();
				SortDirection sortDirection2 = valueOrDefault;
				if (sortDirection2 == SortDirection.asc)
				{
					text = "t.Date ASC, t.Id ASC";
					break;
				}
				if (sortDirection2 == SortDirection.desc)
				{
					text = "t.Date DESC, t.Id ASC";
					break;
				}
			}
			goto default;
		default:
			text = "t.Date DESC, t.Id ASC";
			break;
		}
		if (1 == 0)
		{
		}
		string orderSql = text;
		int offset = (currentPage - 1) * currentSize;
		string pageSql = $"\n;WITH src AS (\n    SELECT t.Id AS Id, t.Date AS TDate,\n           CASE WHEN t.Currency <> N'USD' AND t.Type = N'cash in'  THEN t.Amount\n                WHEN t.Currency <> N'USD' AND t.Type = N'cash out' THEN -t.Amount\n                ELSE CAST(0 AS decimal(18,3)) END AS dIqd,\n           CASE WHEN t.Currency = N'USD' AND t.Type = N'cash in'  THEN t.Amount\n                WHEN t.Currency = N'USD' AND t.Type = N'cash out' THEN -t.Amount\n                ELSE CAST(0 AS decimal(18,3)) END AS dUsd\n    FROM Transactions t\n    WHERE t.BookId = @p0\n),\nrun AS (\n    SELECT Id,\n           SUM(dIqd) OVER (ORDER BY TDate, Id ROWS UNBOUNDED PRECEDING) AS RunIqd,\n           SUM(dUsd) OVER (ORDER BY TDate, Id ROWS UNBOUNDED PRECEDING) AS RunUsd\n    FROM src\n)\nSELECT r.Id, r.RunIqd, r.RunUsd\nFROM run r\nINNER JOIN Transactions t ON t.Id = r.Id\n{"\nLEFT JOIN Categories     c  ON c.Id  = t.CategoryId\nLEFT JOIN Contacts       co ON co.Id = t.ContactId\nLEFT JOIN PaymentMethods pm ON pm.Id = t.PaymentMethodId\nLEFT JOIN Users          u  ON u.Id  = t.UserId"}\n{whereSql}\nORDER BY {orderSql}\nOFFSET {offset} ROWS FETCH NEXT {currentSize} ROWS ONLY";
		List<RunningRow> pageRows = await ExecuteRunningRowsAsync(pageSql, parameters);
		List<Guid> pageIds = pageRows.Select((RunningRow r) => r.Id).ToList();
		List<Transaction> list = ((pageIds.Count != 0) ? (await (from transaction in _context.Transactions.AsNoTracking().AsSplitQuery()
			where pageIds.Contains(transaction.Id)
			select transaction).Include((Transaction transaction) => transaction.Category).Include((Transaction transaction) => transaction.PaymentMethod).Include((Transaction transaction) => transaction.Contact)
			.Include((Transaction transaction) => transaction.User)
			.Include((Transaction transaction) => transaction.CustomFieldValues)
			.ThenInclude((CustomFieldValue cf) => cf.CustomField)
			.Include((Transaction transaction) => transaction.Attachements)
			.ToListAsync()) : new List<Transaction>());
		List<Transaction> pageEntities = list;
		Dictionary<Guid, Transaction> entityById = pageEntities.ToDictionary((Transaction transaction) => transaction.Id);
		List<TransactionDto> paginatedResult = new List<TransactionDto>(pageRows.Count);
		foreach (RunningRow row in pageRows)
		{
			if (entityById.TryGetValue(row.Id, out var t))
			{
				decimal runningBalanceIqd = row.RunIqd;
				decimal runningBalanceUsd = row.RunUsd;
				paginatedResult.Add(new TransactionDto
				{
					Id = t.Id,
					Type = t.Type,
					Amount = t.Amount,
					Currency = t.Currency,
					ExchangeRate = t.ExchangeRate,
					ExchangeDate = t.ExchangeDate,
					Date = t.Date,
					Description = t.Description,
					NewBalance = ((t.Currency == CurrencyCode.USD) ? runningBalanceUsd : runningBalanceIqd),
					NewBalanceIqd = runningBalanceIqd,
					NewBalanceUsd = runningBalanceUsd,
					CategoryId = t.CategoryId,
					Category = ((t.Category == null) ? null : new CategoryDto
					{
						Id = t.Category.Id,
						Name = t.Category.Name,
						BusinessId = t.Category.BusinessId,
						CreatedAt = t.Category.CreatedAt,
						UpdatedAt = t.Category.UpdatedAt
					}),
					PaymentMethod = ((t.PaymentMethod == null) ? null : new PaymentMethodDto
					{
						Id = t.PaymentMethod.Id,
						Name = t.PaymentMethod.Name,
						BusinessId = t.PaymentMethod.BusinessId,
						CreatedAt = t.PaymentMethod.CreatedAt,
						UpdatedAt = t.PaymentMethod.UpdatedAt
					}),
					UserId = t.UserId,
					User = ((t.User == null) ? null : new UserDto
					{
						Id = t.User.Id,
						Name = t.User.Name,
						Email = t.User.Email,
						CreatedAt = t.User.CreatedAt,
						UpdatedAt = t.User.UpdatedAt
					}),
					Contact = ((t.Contact == null) ? null : new ContactDto
					{
						Id = t.Contact.Id,
						BusinessId = t.Contact.BusinessId,
						Name = t.Contact.Name,
						Phone = t.Contact.Phone,
						CreatedAt = t.CreatedAt,
						UpdatedAt = t.UpdatedAt
					}),
					BookId = t.BookId,
					CreatedAt = t.CreatedAt,
					UpdatedAt = t.UpdatedAt,
					CustomFieldValues = t.CustomFieldValues?.Select((CustomFieldValue cf) => new CustomFieldValueDto
					{
						Id = cf.Id,
						Value = cf.Value,
						CustomFieldId = cf.CustomFieldId,
						TransactionId = cf.TransactionId,
						CustomFields = ((cf.CustomField == null) ? null : new CustomFieldDto
						{
							Id = cf.CustomField.Id,
							Key = cf.CustomField.Key,
							BookId = cf.CustomField.BookId,
							IsRequired = cf.CustomField.IsRequired
						})
					}).ToList(),
					Attachments = t.Attachements?.Select((Attachement a) => new AttachmentDto
					{
						Id = a.Id,
						Files = a.Files,
						TransactionId = a.TransactionId
					}).ToList()
				});
				t = null;
			}
		}
		return new PaginatedResponse<TransactionDto>
		{
			TotalRecords = totalRecords,
			Skip = currentPage,
			Take = currentSize,
			Data = paginatedResult
		};
		string Param(object value)
		{
			parameters.Add(value);
			return "@p" + (parameters.Count - 1);
		}
	}

	private static void BindParameters(DbCommand command, IReadOnlyList<object> parameters)
	{
		for (int i = 0; i < parameters.Count; i++)
		{
			DbParameter dbParameter = command.CreateParameter();
			dbParameter.ParameterName = "@p" + i;
			dbParameter.Value = parameters[i] ?? DBNull.Value;
			command.Parameters.Add(dbParameter);
		}
	}

	private async Task<int> ExecuteScalarIntAsync(string sql, IReadOnlyList<object> parameters)
	{
		DbConnection connection = _context.Database.GetDbConnection();
		bool mustClose = connection.State != ConnectionState.Open;
		if (mustClose)
		{
			await connection.OpenAsync();
		}
		int result2;
		try
		{
			int num;
			await using (DbCommand command = connection.CreateCommand())
			{
				command.CommandText = sql;
				BindParameters(command, parameters);
				object result = await command.ExecuteScalarAsync();
				num = ((result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0);
			}
			result2 = num;
		}
		finally
		{
			if (mustClose)
			{
				await connection.CloseAsync();
			}
		}
		return result2;
	}

	private async Task<List<RunningRow>> ExecuteRunningRowsAsync(string sql, IReadOnlyList<object> parameters)
	{
		List<RunningRow> rows = new List<RunningRow>();
		DbConnection connection = _context.Database.GetDbConnection();
		bool mustClose = connection.State != ConnectionState.Open;
		if (mustClose)
		{
			await connection.OpenAsync();
		}
		try
		{
			await using DbCommand command = connection.CreateCommand();
			command.CommandText = sql;
			BindParameters(command, parameters);
			await using DbDataReader reader = await command.ExecuteReaderAsync();
			while (await reader.ReadAsync())
			{
				rows.Add(new RunningRow
				{
					Id = reader.GetGuid(0),
					RunIqd = reader.GetDecimal(1),
					RunUsd = reader.GetDecimal(2)
				});
			}
		}
		finally
		{
			if (mustClose)
			{
				await connection.CloseAsync();
			}
		}
		return rows;
	}

	public async Task<List<TransactionDto>> GetAllTransactionsRawByBookIdAsync(Guid bookId)
	{
		return (await (from t in (from t in _context.Transactions.AsNoTracking().AsSplitQuery()
				where t.BookId == bookId
				select t).Include((Transaction t) => t.Category).Include((Transaction t) => t.Contact).Include((Transaction t) => t.User)
				.Include((Transaction t) => t.CustomFieldValues)
				.ThenInclude((CustomFieldValue cf) => cf.CustomField)
			orderby t.Date descending
			select t).ToListAsync()).Select((Transaction t) => new TransactionDto
		{
			Type = t.Type,
			Amount = t.Amount,
			Currency = t.Currency,
			ExchangeRate = t.ExchangeRate,
			ExchangeDate = t.ExchangeDate,
			Date = t.Date,
			Description = t.Description,
			Category = ((t.Category == null) ? null : new CategoryDto
			{
				Name = t.Category.Name
			}),
			User = ((t.User == null) ? null : new UserDto
			{
				Name = t.User.Name,
				Email = t.User.Email
			}),
			Contact = ((t.Contact == null) ? null : new ContactDto
			{
				Name = t.Contact.Name,
				Phone = t.Contact.Phone
			}),
			CustomFieldValues = t.CustomFieldValues?.Select((CustomFieldValue cf) => new CustomFieldValueDto
			{
				Id = cf.Id,
				Value = cf.Value,
				CustomFields = ((cf.CustomField == null) ? null : new CustomFieldDto
				{
					Key = cf.CustomField.Key
				})
			}).ToList()
		}).ToList();
	}

	public async Task<bool> UpdateTransactionAsync(UpdateTransactionDto dto, Guid Id)
	{
		try
		{
			Transaction transaction = await _context.Transactions.Include((Transaction t) => t.CustomFieldValues).Include((Transaction t) => t.Attachements).FirstOrDefaultAsync((Transaction t) => t.Id == Id);
			if (transaction == null)
			{
				return false;
			}
			decimal oldAmount = transaction.Amount;
			decimal? oldRate = transaction.ExchangeRate;
			DateTime? oldExchangeDate = transaction.ExchangeDate;
			string oldType = transaction.Type;
			CurrencyCode oldCurrency = transaction.Currency;
			ResolvedTransactionValues resolved = TransactionValidator.Resolve(dto, transaction);
			transaction.Type = resolved.Type ?? transaction.Type;
			transaction.Date = resolved.Date ?? transaction.Date;
			transaction.Amount = resolved.Amount;
			transaction.Currency = resolved.Currency;
			bool isUsd = transaction.Currency == CurrencyCode.USD;
			bool isWithdrawal = TransactionTypes.Normalize(transaction.Type) == "cash out";
			transaction.ExchangeRate = (isUsd ? resolved.ExchangeRate : ((decimal?)null));
			transaction.ExchangeDate = ((isUsd && !isWithdrawal) ? resolved.ExchangeDate : ((DateTime?)null));
			transaction.Description = dto.Description ?? transaction.Description;
			transaction.CategoryId = dto.CategoryId ?? transaction.CategoryId;
			transaction.ContactId = dto.ContactId ?? transaction.ContactId;
			transaction.PaymentMethodId = dto.PaymentMethodId ?? transaction.PaymentMethodId;
			transaction.UpdatedAt = DateTime.UtcNow;
			List<CustomFieldValueCreateDto> customFieldValues = new List<CustomFieldValueCreateDto>();
			if (!string.IsNullOrWhiteSpace(dto.CustomFieldValues))
			{
				customFieldValues = JsonSerializer.Deserialize<List<CustomFieldValueCreateDto>>(dto.CustomFieldValues, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});
			}
			if (transaction.CustomFieldValues != null && transaction.CustomFieldValues.Any())
			{
				_context.CustomFieldValues.RemoveRange(transaction.CustomFieldValues);
			}
			if (customFieldValues?.Any() ?? false)
			{
				IEnumerable<CustomFieldValue> newCustomFieldValues = customFieldValues.Select((CustomFieldValueCreateDto cfvDto) => new CustomFieldValue
				{
					Value = cfvDto.Value,
					CustomFieldId = cfvDto.CustomFieldId,
					TransactionId = transaction.Id
				});
				await _context.CustomFieldValues.AddRangeAsync(newCustomFieldValues);
			}
			if (dto.Files != null && dto.Files.Any())
			{
				string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
				if (transaction.Attachements != null && transaction.Attachements.Any())
				{
					foreach (Attachement attachment in transaction.Attachements)
					{
						if (string.IsNullOrEmpty(attachment.Files))
						{
							continue;
						}
						string filePath = Path.Combine(uploadPath, attachment.Files);
						if (File.Exists(filePath))
						{
							try
							{
								File.Delete(filePath);
							}
							catch (Exception ex)
							{
								Exception ex2 = ex;
								Console.WriteLine("Failed to delete file " + attachment.Files + ": " + ex2.Message);
							}
						}
					}
					_context.Attachements.RemoveRange(transaction.Attachements);
				}
				foreach (string fileName in await UploadFilesAsync(dto.Files, uploadPath))
				{
					Attachement attachment2 = new Attachement
					{
						Files = fileName,
						TransactionId = transaction.Id
					};
					_context.Attachements.Add(attachment2);
				}
			}
			await _context.SaveChangesAsync();
			if (transaction.Amount != oldAmount || transaction.Currency != oldCurrency || !string.Equals(transaction.Type, oldType, StringComparison.OrdinalIgnoreCase) || !(transaction.ExchangeRate == oldRate) || transaction.ExchangeDate != oldExchangeDate)
			{
				TransactionHistory transactionHistory = new TransactionHistory
				{
					Operation = "PUT",
					Description = transaction.Description,
					Amount = transaction.Amount,
					Type = transaction.Type,
					From = oldAmount,
					To = transaction.Amount,
					ExchangeRate = transaction.ExchangeRate,
					ExchangeDate = transaction.ExchangeDate,
					TransactionId = transaction.Id,
					BookId = transaction.BookId,
					UserId = transaction.UserId
				};
				_context.TransactionHistories.Add(transactionHistory);
				await _context.SaveChangesAsync();
			}
			return true;
		}
		catch (Exception ex)
		{
			Exception ex3 = ex;
			throw new InvalidOperationException("تعذ\u0651ر تحديث الحركة. تحقق من البيانات وحاول مرة أخرى.", ex3);
		}
	}

	public async Task<Guid> DuplicateTransactionToAnotherBookAsync(Guid transactionId, Guid targetBookId)
	{
		Transaction transaction = await _context.Transactions.Include((Transaction t) => t.CustomFieldValues).Include((Transaction t) => t.Attachements).FirstOrDefaultAsync((Transaction t) => t.Id == transactionId);
		if (transaction == null)
		{
			throw new InvalidOperationException("Transaction not found");
		}
		Transaction newTransaction = new Transaction
		{
			Type = transaction.Type,
			Date = transaction.Date,
			Description = transaction.Description,
			Amount = transaction.Amount,
			Currency = transaction.Currency,
			CategoryId = transaction.CategoryId,
			ContactId = transaction.ContactId,
			PaymentMethodId = transaction.PaymentMethodId,
			UserId = transaction.UserId,
			BookId = targetBookId
		};
		await _context.Transactions.AddAsync(newTransaction);
		await _context.SaveChangesAsync();
		if (transaction.CustomFieldValues != null && transaction.CustomFieldValues.Any())
		{
			List<CustomFieldValue> newCustomFields = transaction.CustomFieldValues.Select((CustomFieldValue cf) => new CustomFieldValue
			{
				Value = cf.Value,
				CustomFieldId = cf.CustomFieldId,
				TransactionId = newTransaction.Id
			}).ToList();
			await _context.CustomFieldValues.AddRangeAsync(newCustomFields);
		}
		if (transaction.Attachements != null && transaction.Attachements.Any())
		{
			string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
			List<Attachement> newAttachments = new List<Attachement>();
			foreach (Attachement att in transaction.Attachements)
			{
				if (!string.IsNullOrWhiteSpace(att.Files))
				{
					string originalFilePath = Path.Combine(uploadsFolder, att.Files);
					if (File.Exists(originalFilePath))
					{
						string newFileName = $"{Guid.NewGuid()}{Path.GetExtension(att.Files)}";
						string newFilePath = Path.Combine(uploadsFolder, newFileName);
						File.Copy(originalFilePath, newFilePath);
						newAttachments.Add(new Attachement
						{
							Files = newFileName,
							TransactionId = newTransaction.Id
						});
					}
				}
			}
			if (newAttachments.Any())
			{
				await _context.Attachements.AddRangeAsync(newAttachments);
			}
		}
		await _context.SaveChangesAsync();
		return newTransaction.Id;
	}
}
