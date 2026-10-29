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
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Services;
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

    private readonly IFileStorage _files;

    public TransactionRepository(ApplicationDbContext context, IFileStorage files)
        : base(context)
    {
        _context = context;
        _files = files;
    }

    public async Task<bool> CreateTransactionAsync(CreateTransactionDto dto, Guid userId)
    {
        // تحليل آمن: الصيغة المعطوبة لا تُحوّل إلى خطأ خادم.
        // المدقّق يردّ بـ400 قبل الوصول إلى هنا، وهذه حراسة ثانية في العمق.
        CustomFieldValuesParser.TryParse(dto.CustomFieldValues, out List<CustomFieldValueCreateDto> customFieldValues, out _);
        string normalizedType = TransactionTypes.Normalize(dto.Type) ?? dto.Type;
        bool isUsd = dto.Currency == CurrencyCode.USD;
        decimal? exchangeRate = (isUsd ? dto.ExchangeRate : ((decimal?)null));
        DateTime? exchangeDate = ((isUsd && normalizedType != "cash out") ? dto.ExchangeDate : ((DateTime?)null));
        // الملفات تُرفع أولاً، ثم يُحفظ القيد بمرفقاته وقيمه وسجله في عملية حفظ واحدة:
        // إما أن يُحفظ كل شيء أو لا شيء، فلا يبقى قيد ناقص بلا سجل أو بلا حقوله.
        List<string> uploaded = new List<string>();
        if (dto.Files != null && dto.Files.Any())
        {
            uploaded = await _files.SaveAsync(dto.Files, UploadKind.Attachment);
        }
        try
        {
            Transaction transaction = new Transaction
            {
                Type = normalizedType,
                Date = dto.Date,
                Description = dto.Description,
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
            foreach (CustomFieldValueCreateDto cfvDto in customFieldValues ?? new List<CustomFieldValueCreateDto>())
            {
                _context.CustomFieldValues.Add(new CustomFieldValue
                {
                    Value = cfvDto.Value,
                    CustomFieldId = cfvDto.CustomFieldId,
                    TransactionId = transaction.Id
                });
            }
            foreach (string fileName in uploaded)
            {
                _context.Attachements.Add(new Attachement
                {
                    Files = fileName,
                    TransactionId = transaction.Id
                });
            }
            _context.TransactionHistories.Add(new TransactionHistory
            {
                Operation = "POST",
                Description = dto.Description,
                Amount = dto.Amount,
                Type = transaction.Type,
                ExchangeRate = transaction.ExchangeRate,
                ExchangeDate = transaction.ExchangeDate,
                TransactionId = transaction.Id,
                BookId = dto.BookId,
                UserId = userId
            });
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _files.Delete(uploaded);
            throw new InvalidOperationException("تعذّر حفظ الحركة. تحقق من البيانات وحاول مرة أخرى.", ex);
        }
    }

    /// <summary>
    /// أرشفة قيد دون رجعة: يختفي من القوائم والأرصدة ويبقى في قاعدة البيانات بمرفقاته.
    /// يُسجَّل في سجل الحركات باسم من نفّذ الأرشفة فعلاً.
    /// </summary>
    public async Task<bool> ArchiveTransactionAsync(Guid transactionId, Guid archivedByUserId, string? reason = null)
    {
        Transaction transaction = await _context.Transactions.FirstOrDefaultAsync((Transaction t) => t.Id == transactionId);
        if (transaction == null)
        {
            return false;
        }
        transaction.IsArchived = true;
        transaction.ArchivedAt = DateTime.UtcNow;
        transaction.ArchivedByUserId = archivedByUserId;
        transaction.ArchiveReason = ArchiveText.Reason(reason);
        transaction.UpdatedAt = DateTime.UtcNow;
        _context.TransactionHistories.Add(new TransactionHistory
        {
            Operation = "ARCHIVE",
            Description = transaction.Description,
            Amount = transaction.Amount,
            Type = transaction.Type,
            ExchangeRate = transaction.ExchangeRate,
            ExchangeDate = transaction.ExchangeDate,
            TransactionId = transaction.Id,
            BookId = transaction.BookId,
            UserId = archivedByUserId,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PaginatedResponse<TransactionDto>> GetAllTransactionsByBookIdAsync(Guid bookId, int? skip = 1, int? take = 25, decimal? amount = null, string searchCategory = null, string searchContact = null, string searchPaymentMethod = null, string searchType = null, string searchUser = null, DateTime? startDate = null, DateTime? endDate = null, SortField? sortBy = null, SortDirection? sortDirection = SortDirection.asc)
    {
        int currentPage = Math.Max(1, skip ?? 1);
        int currentSize = Math.Min(take ?? 25, 500);
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
        List<string> filters = new List<string> { "t.BookId = " + Param(bookId), "t.IsArchived = 0" };
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
        // الربط بالجداول الأخرى لا يلزم للعدّ إلا عند البحث بأسمائها.
        bool needsJoins = !string.IsNullOrEmpty(searchCategory) || !string.IsNullOrEmpty(searchContact) || !string.IsNullOrEmpty(searchPaymentMethod) || !string.IsNullOrEmpty(searchUser);
        string countJoins = needsJoins ? "LEFT JOIN Categories     c  ON c.Id  = t.CategoryId\nLEFT JOIN Contacts       co ON co.Id = t.ContactId\nLEFT JOIN PaymentMethods pm ON pm.Id = t.PaymentMethodId\nLEFT JOIN Users          u  ON u.Id  = t.UserId\n" : string.Empty;
        int totalRecords = await ExecuteScalarIntAsync("SELECT COUNT(*) FROM Transactions t \n" + countJoins + whereSql, parameters);
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
        string pageSql = $"\n;WITH src AS (\n    SELECT t.Id AS Id, t.Date AS TDate,\n           CASE WHEN t.Currency <> N'USD' AND t.Type = N'cash in'  THEN t.Amount\n                WHEN t.Currency <> N'USD' AND t.Type = N'cash out' THEN -t.Amount\n                ELSE CAST(0 AS decimal(18,3)) END AS dIqd,\n           CASE WHEN t.Currency = N'USD' AND t.Type = N'cash in'  THEN t.Amount\n                WHEN t.Currency = N'USD' AND t.Type = N'cash out' THEN -t.Amount\n                ELSE CAST(0 AS decimal(18,3)) END AS dUsd\n    FROM Transactions t\n    WHERE t.BookId = @p0 AND t.IsArchived = 0\n),\nrun AS (\n    SELECT Id,\n           SUM(dIqd) OVER (ORDER BY TDate, Id ROWS UNBOUNDED PRECEDING) AS RunIqd,\n           SUM(dUsd) OVER (ORDER BY TDate, Id ROWS UNBOUNDED PRECEDING) AS RunUsd\n    FROM src\n)\nSELECT r.Id, r.RunIqd, r.RunUsd\nFROM run r\nINNER JOIN Transactions t ON t.Id = r.Id\n{"\nLEFT JOIN Categories     c  ON c.Id  = t.CategoryId\nLEFT JOIN Contacts       co ON co.Id = t.ContactId\nLEFT JOIN PaymentMethods pm ON pm.Id = t.PaymentMethodId\nLEFT JOIN Users          u  ON u.Id  = t.UserId"}\n{whereSql}\nORDER BY {orderSql}\nOFFSET {offset} ROWS FETCH NEXT {currentSize} ROWS ONLY";
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
                        Files = _files.SignedName(a.Files),
                        FileName = a.Files,
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

    public async Task<bool> UpdateTransactionAsync(UpdateTransactionDto dto, Guid Id, Guid editorUserId)
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
            // عند فشل التحليل لا تُمسّ القيم القائمة — وإلا تحوّلت صيغة معطوبة إلى حذف بيانات.
            bool customFieldValuesAccepted = CustomFieldValuesParser.TryParse(dto.CustomFieldValues, out customFieldValues, out _);
            if (customFieldValuesAccepted && transaction.CustomFieldValues != null && transaction.CustomFieldValues.Any())
            {
                _context.CustomFieldValues.RemoveRange(transaction.CustomFieldValues);
            }
            if (customFieldValuesAccepted && (customFieldValues?.Any() ?? false))
            {
                IEnumerable<CustomFieldValue> newCustomFieldValues = customFieldValues.Select((CustomFieldValueCreateDto cfvDto) => new CustomFieldValue
                {
                    Value = cfvDto.Value,
                    CustomFieldId = cfvDto.CustomFieldId,
                    TransactionId = transaction.Id
                });
                await _context.CustomFieldValues.AddRangeAsync(newCustomFieldValues);
            }
            List<string> uploaded = new List<string>();
            if (dto.Files != null && dto.Files.Any())
            {
                // المرفقات القديمة تُؤرشف ولا تُحذف ملفاتها، فلا يضيع شيء لو فشل الحفظ.
                foreach (Attachement old in transaction.Attachements ?? new List<Attachement>())
                {
                    old.IsArchived = true;
                    old.ArchivedAt = DateTime.UtcNow;
                    old.ArchivedByUserId = editorUserId;
                    old.ArchiveReason = "استُبدل بمرفق جديد عند تعديل القيد";
                }
                uploaded = await _files.SaveAsync(dto.Files, UploadKind.Attachment);
                foreach (string fileName in uploaded)
                {
                    _context.Attachements.Add(new Attachement
                    {
                        Files = fileName,
                        TransactionId = transaction.Id
                    });
                }
            }
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
                    UserId = editorUserId
                };
                _context.TransactionHistories.Add(transactionHistory);
            }
            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                _files.Delete(uploaded);
                throw;
            }
            return true;
        }
        catch (Exception ex)
        {
            Exception ex3 = ex;
            throw new InvalidOperationException("تعذ\u0651ر تحديث الحركة. تحقق من البيانات وحاول مرة أخرى.", ex3);
        }
    }

    public async Task<Guid> DuplicateTransactionToAnotherBookAsync(Guid transactionId, Guid targetBookId, Guid userId)
    {
        Transaction transaction = await _context.Transactions.Include((Transaction t) => t.CustomFieldValues).Include((Transaction t) => t.Attachements).FirstOrDefaultAsync((Transaction t) => t.Id == transactionId);
        if (transaction == null)
        {
            throw new InvalidOperationException("Transaction not found");
        }
        // سعر الصرف وتاريخه يُنسخان أيضاً: نسخ حركة دولار بدونهما يُفسد حسابات التحويل.
        Transaction newTransaction = new Transaction
        {
            Type = transaction.Type,
            Date = transaction.Date,
            Description = transaction.Description,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            ExchangeRate = transaction.ExchangeRate,
            ExchangeDate = transaction.ExchangeDate,
            CategoryId = transaction.CategoryId,
            ContactId = transaction.ContactId,
            PaymentMethodId = transaction.PaymentMethodId,
            UserId = userId,
            BookId = targetBookId
        };
        _context.Transactions.Add(newTransaction);
        foreach (CustomFieldValue cf in transaction.CustomFieldValues ?? new List<CustomFieldValue>())
        {
            _context.CustomFieldValues.Add(new CustomFieldValue
            {
                Value = cf.Value,
                CustomFieldId = cf.CustomFieldId,
                TransactionId = newTransaction.Id
            });
        }
        List<string> copied = new List<string>();
        foreach (Attachement att in transaction.Attachements ?? new List<Attachement>())
        {
            string? newFileName = string.IsNullOrWhiteSpace(att.Files) ? null : _files.Copy(att.Files);
            if (newFileName == null)
            {
                continue;
            }
            copied.Add(newFileName);
            _context.Attachements.Add(new Attachement
            {
                Files = newFileName,
                TransactionId = newTransaction.Id
            });
        }
        _context.TransactionHistories.Add(new TransactionHistory
        {
            Operation = "POST",
            Description = newTransaction.Description,
            Amount = newTransaction.Amount,
            Type = newTransaction.Type,
            ExchangeRate = newTransaction.ExchangeRate,
            ExchangeDate = newTransaction.ExchangeDate,
            TransactionId = newTransaction.Id,
            BookId = targetBookId,
            UserId = userId
        });
        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            _files.Delete(copied);
            throw;
        }
        return newTransaction.Id;
    }
}
