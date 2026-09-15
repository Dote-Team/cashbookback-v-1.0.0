using cashbook.Data;
using cashbook.Dto.Book;
using cashbook.Dto.category;
using cashbook.Dto.contact;
using cashbook.Dto;
using cashbook.Dto.transaction;
using cashbook.Dto.user;
using cashbook.Interfaces;
using cashbook.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using cashbook.Dto.paymentMethod;

namespace cashbook.Repositories
{
    public class TransactionRepository : Repository<Transaction>, ITransactionRepository
    {

        private readonly ApplicationDbContext _context;
        public TransactionRepository(ApplicationDbContext context, IConfiguration configuration) : base(context)
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

                var transaction = new Transaction
                {
                    Type = dto.Type,
                    Date = dto.Date,
                    Description = dto.Description != null ? dto.Description : null,
                    Amount = dto.Amount,
                    CategoryId = dto.CategoryId,
                    BookId = dto.BookId,
                    ContactId = dto.ContactId,
                    PaymentMethodId = dto.PaymentMethodId,
                    UserId = userId,

                };

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync();

                if (customFieldValues != null && customFieldValues.Any())
                {
                    var entities = customFieldValues.Select(cfvDto => new CustomFieldValue
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
                    string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

                    var uploadedFileNames = await UploadFilesAsync(dto.Files, uploadPath);

                    foreach (var fileName in uploadedFileNames)
                    {
                        var attachment = new Attachement
                        {
                            Files = fileName,
                            TransactionId = transaction.Id
                        };
                        _context.Attachements.Add(attachment);
                    }
                    await _context.SaveChangesAsync();
                }

                var transactionHistory = new TransactionHistory
                {
                    Operation = "POST",
                    Description = dto.Description != null ? dto.Description : null,
                    Amount = dto.Amount,
                    Type = dto.Type,
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
                throw new InvalidOperationException($"Error: {ex.Message}\nStack: {ex.StackTrace}", ex);

            }
        }


        public async Task<bool> DeleteTransactionAsync(Guid transactionId)
        {
            // جلب الـ Transaction مع المرفقات والقيم المخصصة
            var transaction = await _context.Transactions
                .Include(t => t.CustomFieldValues)
                .Include(t => t.Attachements)
                .FirstOrDefaultAsync(t => t.Id == transactionId);

            if (transaction == null)
                return false;

            // 1. إنشاء سجل الحذف في TransactionHistory قبل حذف الـ Transaction
            var transactionHistory = new TransactionHistory
            {
                Operation = "DELETE",
                Description = transaction.Description,
                Amount = transaction.Amount,
                Type = transaction.Type,
                From = null,
                To = null,
                TransactionId = null, // null لأننا نريد الاحتفاظ بالسجل بعد حذف الـ Transaction
                BookId = transaction.BookId,
                UserId = transaction.UserId,
                CreatedAt = DateTime.UtcNow // تأكد إذا عندك CreatedAt
            };
            _context.TransactionHistories.Add(transactionHistory);

            // 2. حذف الملفات المرفقة من النظام
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

            if (transaction.Attachements != null && transaction.Attachements.Any())
            {
                foreach (var attachment in transaction.Attachements)
                {
                    if (string.IsNullOrEmpty(attachment.Files))
                        continue;

                    var filePath = Path.Combine(uploadsFolder, attachment.Files);
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to delete file {attachment.Files}: {ex.Message}");
                        }
                    }
                }

                // حذف المرفقات من قاعدة البيانات
                _context.Attachements.RemoveRange(transaction.Attachements);
            }

            // 3. حذف القيم المخصصة إذا موجودة
            if (transaction.CustomFieldValues != null && transaction.CustomFieldValues.Any())
                _context.CustomFieldValues.RemoveRange(transaction.CustomFieldValues);

            // 4. حذف الـ Transaction نفسه
            _context.Transactions.Remove(transaction);

            // 5. حفظ كل التغييرات مرة واحدة لتجنب مشاكل FK
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<PaginatedResponse<TransactionDto>> GetAllTransactionsByBookIdAsync(
            Guid bookId,
            int? skip = 1,
            int? take = 25,
            decimal? amount = null,
            string searchCategory = null,
            string searchContact = null,
            string searchPaymentMethod = null,
            string searchType = null,
            string searchUser = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            SortField? sortBy = null,
            SortDirection? sortDirection = SortDirection.asc)
        {
            int currentPage = skip.GetValueOrDefault(1);
            int currentSize = take.GetValueOrDefault(25);

            // Get all transactions by book in date ASC order for running balance
            var allTransactions = await _context.Transactions
                .Where(t => t.BookId == bookId)
                .Include(t => t.Category)
                .Include(t => t.PaymentMethod)
                .Include(t => t.Contact)
                .Include(t => t.User)
                .Include(t => t.CustomFieldValues).ThenInclude(cf => cf.CustomField)
                .Include(t => t.Attachements)
                .OrderBy(t => t.Date)
                .ToListAsync();

            decimal runningBalance = 0;
            var allDtos = new List<TransactionDto>();

            foreach (var t in allTransactions)
            {
                if (t.Type.Equals("cash in", StringComparison.InvariantCultureIgnoreCase))
                    runningBalance += t.Amount;
                else if (t.Type.Equals("cash out", StringComparison.InvariantCultureIgnoreCase))
                    runningBalance -= t.Amount;

                allDtos.Add(new TransactionDto
                {
                    Id = t.Id,
                    Type = t.Type,
                    Amount = t.Amount,
                    Date = t.Date,
                    Description = t.Description,
                    NewBalance = runningBalance,
                    CategoryId = t.CategoryId,
                    Category = t.Category == null ? null : new CategoryDto
                    {
                        Id = t.Category.Id,
                        Name = t.Category.Name,
                        BusinessId = t.Category.BusinessId,
                        CreatedAt = t.Category.CreatedAt,
                        UpdatedAt = t.Category.UpdatedAt
                    },
                    PaymentMethod = t.PaymentMethod == null ? null : new PaymentMethodDto
                    {
                        Id = t.PaymentMethod.Id,
                        Name = t.PaymentMethod.Name,
                        BusinessId = t.PaymentMethod.BusinessId,
                        CreatedAt = t.PaymentMethod.CreatedAt,
                        UpdatedAt = t.PaymentMethod.UpdatedAt
                    },
                    UserId = t.UserId,
                    User = t.User == null ? null : new UserDto
                    {
                        Id = t.User.Id,
                        Name = t.User.Name,
                        Email = t.User.Email,
                        CreatedAt = t.User.CreatedAt,
                        UpdatedAt = t.User.UpdatedAt
                    },
                    Contact = t.Contact == null ? null : new ContactDto
                    {
                        Id = t.Contact.Id,
                        BusinessId = t.Contact.BusinessId,
                        Name = t.Contact.Name,
                        Phone = t.Contact.Phone,
                        CreatedAt = t.CreatedAt,
                        UpdatedAt = t.UpdatedAt
                    },
                    BookId = t.BookId,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt,
                    CustomFieldValues = t.CustomFieldValues?.Select(cf => new CustomFieldValueDto
                    {
                        Id = cf.Id,
                        Value = cf.Value,
                        CustomFieldId = cf.CustomFieldId,
                        TransactionId = cf.TransactionId,
                        CustomFields = cf.CustomField == null ? null : new CustomFieldDto
                        {
                            Id = cf.CustomField.Id,
                            Key = cf.CustomField.Key,
                            BookId = cf.CustomField.BookId,
                            IsRequired = cf.CustomField.IsRequired
                        }
                    }).ToList(),
                    Attachments = t.Attachements?.Select(a => new AttachmentDto
                    {
                        Id = a.Id,
                        Files = a.Files,
                        TransactionId = a.TransactionId
                    }).ToList()
                });
            }

            // Apply filters
            var filteredDtos = allDtos.AsEnumerable(); // ✅ Use in-memory filtering/sorting

            if (amount.HasValue)
                filteredDtos = filteredDtos.Where(t => t.Amount == amount.Value);

            if (!string.IsNullOrEmpty(searchCategory))
                filteredDtos = filteredDtos.Where(t => t.Category != null &&
                    t.Category.Name.ToLower().Contains(searchCategory.ToLower()));

            if (!string.IsNullOrEmpty(searchContact))
                filteredDtos = filteredDtos.Where(t => t.Contact != null &&
                    t.Contact.Name.ToLower().Contains(searchContact.ToLower()));

            if (!string.IsNullOrEmpty(searchPaymentMethod))
                filteredDtos = filteredDtos.Where(t => t.PaymentMethod != null &&
                    t.PaymentMethod.Name.ToLower().Contains(searchPaymentMethod.ToLower()));

            if (!string.IsNullOrEmpty(searchUser))
                filteredDtos = filteredDtos.Where(t =>
                    t.User != null &&
                    (t.User.Name.ToLower().Contains(searchUser.ToLower()) ||
                     t.User.Email.ToLower().Contains(searchUser.ToLower())));

            if (!string.IsNullOrEmpty(searchType))
                filteredDtos = filteredDtos.Where(t => t.Type.ToLower() == searchType.ToLower());

            if (startDate.HasValue)
                filteredDtos = filteredDtos.Where(t => t.Date >= startDate.Value);

            if (endDate.HasValue)
                filteredDtos = filteredDtos.Where(t => t.Date <= endDate.Value);

            // Apply sorting
            filteredDtos = (sortBy, sortDirection) switch
            {
                (SortField.Category, SortDirection.asc) => filteredDtos.OrderBy(t => t.Category?.Name),
                (SortField.Category, SortDirection.desc) => filteredDtos.OrderByDescending(t => t.Category?.Name),

                (SortField.PaymentMethod, SortDirection.asc) => filteredDtos.OrderBy(t => t.PaymentMethod?.Name),
                (SortField.PaymentMethod, SortDirection.desc) => filteredDtos.OrderByDescending(t => t.PaymentMethod?.Name),

                (SortField.Description, SortDirection.asc) => filteredDtos.OrderBy(t => t.Description),
                (SortField.Description, SortDirection.desc) => filteredDtos.OrderByDescending(t => t.Description),

                (SortField.Amount, SortDirection.asc) => filteredDtos.OrderBy(t => t.Amount),
                (SortField.Amount, SortDirection.desc) => filteredDtos.OrderByDescending(t => t.Amount),

                (SortField.NewBalance, SortDirection.asc) => filteredDtos.OrderBy(t => t.NewBalance),
                (SortField.NewBalance, SortDirection.desc) => filteredDtos.OrderByDescending(t => t.NewBalance),

                (SortField.Date, SortDirection.asc) => filteredDtos.OrderBy(t => t.Date),
                (SortField.Date, SortDirection.desc) => filteredDtos.OrderByDescending(t => t.Date),

                _ => filteredDtos.OrderByDescending(t => t.Date)
            };

            int totalRecords = filteredDtos.Count();

            var paginatedResult = filteredDtos
                .Skip((currentPage - 1) * currentSize)
                .Take(currentSize)
                .ToList();

            return new PaginatedResponse<TransactionDto>
            {
                TotalRecords = totalRecords,
                Skip = currentPage,
                Take = currentSize,
                Data = paginatedResult
            };
        }


        public async Task<List<TransactionDto>> GetAllTransactionsRawByBookIdAsync(Guid bookId)
        {
            var allTransactions = await _context.Transactions
                .Where(t => t.BookId == bookId)
                .Include(t => t.Category)
                .Include(t => t.Contact)
                .Include(t => t.User)
                .Include(t => t.CustomFieldValues).ThenInclude(cf => cf.CustomField)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            var result = allTransactions.Select(t => new TransactionDto
            {
                Type = t.Type,
                Amount = t.Amount,
                Date = t.Date,
                Description = t.Description,
                Category = t.Category == null ? null : new CategoryDto
                {
                    Name = t.Category.Name,
                },
                User = t.User == null ? null : new UserDto
                {
                    Name = t.User.Name,
                    Email = t.User.Email,
                },
                Contact = t.Contact == null ? null : new ContactDto
                {
                    Name = t.Contact.Name,
                    Phone = t.Contact.Phone,
                },
                CustomFieldValues = t.CustomFieldValues?.Select(cf => new CustomFieldValueDto
                {
                    Id = cf.Id,
                    Value = cf.Value,
                    CustomFields = cf.CustomField == null ? null : new CustomFieldDto
                    {
                        Key = cf.CustomField.Key,
                    }
                }).ToList(),

            }).ToList();

            return result;
        }


        public async Task<bool> UpdateTransactionAsync(UpdateTransactionDto dto, Guid Id)
        {
            try
            {
                // Get the existing transaction with related data
                var transaction = await _context.Transactions
                    .Include(t => t.CustomFieldValues)
                    .Include(t => t.Attachements)
                    .FirstOrDefaultAsync(t => t.Id == Id);

                if (transaction == null)
                    return false;

                var oldAmount = transaction.Amount;


                // Update basic transaction properties
                transaction.Type = dto.Type;
                transaction.Date = dto.Date ?? transaction.Date;
                transaction.Description = dto.Description ?? transaction.Description;
                transaction.Amount = dto.Amount ?? transaction.Amount;
                transaction.CategoryId = dto.CategoryId ?? transaction.CategoryId;
                transaction.ContactId = dto.ContactId ?? transaction.ContactId;
                transaction.PaymentMethodId = dto.PaymentMethodId ?? transaction.PaymentMethodId;
                transaction.UpdatedAt = DateTime.UtcNow;

                // Handle custom field values
                List<CustomFieldValueCreateDto> customFieldValues = new List<CustomFieldValueCreateDto>();
                if (!string.IsNullOrWhiteSpace(dto.CustomFieldValues))
                {
                    customFieldValues = JsonSerializer.Deserialize<List<CustomFieldValueCreateDto>>(
                        dto.CustomFieldValues,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }

                // Remove existing custom field values
                if (transaction.CustomFieldValues != null && transaction.CustomFieldValues.Any())
                {
                    _context.CustomFieldValues.RemoveRange(transaction.CustomFieldValues);
                }

                // Add new custom field values
                if (customFieldValues != null && customFieldValues.Any())
                {
                    var newCustomFieldValues = customFieldValues.Select(cfvDto => new CustomFieldValue
                    {
                        Value = cfvDto.Value,
                        CustomFieldId = cfvDto.CustomFieldId,
                        TransactionId = transaction.Id
                    });
                    await _context.CustomFieldValues.AddRangeAsync(newCustomFieldValues);
                }

                // Handle attachments
                if (dto.Files != null && dto.Files.Any())
                {
                    string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

                    // Remove existing attachments and files
                    if (transaction.Attachements != null && transaction.Attachements.Any())
                    {
                        foreach (var attachment in transaction.Attachements)
                        {
                            if (!string.IsNullOrEmpty(attachment.Files))
                            {
                                var filePath = Path.Combine(uploadPath, attachment.Files);
                                if (File.Exists(filePath))
                                {
                                    try
                                    {
                                        File.Delete(filePath);
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Failed to delete file {attachment.Files}: {ex.Message}");
                                    }
                                }
                            }
                        }
                        _context.Attachements.RemoveRange(transaction.Attachements);
                    }

                    // Add new attachments
                    var uploadedFileNames = await UploadFilesAsync(dto.Files, uploadPath);
                    foreach (var fileName in uploadedFileNames)
                    {
                        var attachment = new Attachement
                        {
                            Files = fileName,
                            TransactionId = transaction.Id
                        };
                        _context.Attachements.Add(attachment);
                    }
                }

                await _context.SaveChangesAsync();

                if (dto.Amount.HasValue && dto.Amount.Value != oldAmount)
                {
                    var transactionHistory = new TransactionHistory
                    {
                        Operation = "PUT",
                        Description = transaction.Description,
                        Amount = transaction.Amount,
                        Type = transaction.Type,
                        From = oldAmount,
                        To = dto.Amount.Value,
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
                throw new InvalidOperationException($"Error: {ex.Message}\nStack: {ex.StackTrace}", ex);
            }
        }


        public async Task<Guid> DuplicateTransactionToAnotherBookAsync(Guid transactionId, Guid targetBookId)
        {
            var transaction = await _context.Transactions
                .Include(t => t.CustomFieldValues)
                .Include(t => t.Attachements)
                .FirstOrDefaultAsync(t => t.Id == transactionId);

            if (transaction == null)
                throw new InvalidOperationException("Transaction not found");

            var newTransaction = new Transaction
            {
                Type = transaction.Type,
                Date = transaction.Date,
                Description = transaction.Description,
                Amount = transaction.Amount,
                CategoryId = transaction.CategoryId,
                ContactId = transaction.ContactId,
                PaymentMethodId = transaction.PaymentMethodId,
                UserId = transaction.UserId,
                BookId = targetBookId,
            };

            await _context.Transactions.AddAsync(newTransaction);
            await _context.SaveChangesAsync();

            // Clone CustomFieldValues
            if (transaction.CustomFieldValues != null && transaction.CustomFieldValues.Any())
            {
                var newCustomFields = transaction.CustomFieldValues.Select(cf => new CustomFieldValue
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
                var newAttachments = new List<Attachement>();

                foreach (var att in transaction.Attachements)
                {
                    if (string.IsNullOrWhiteSpace(att.Files))
                        continue;

                    var originalFilePath = Path.Combine(uploadsFolder, att.Files);
                    if (!File.Exists(originalFilePath))
                        continue; // skip if original file doesn't exist

                    // Create new unique filename
                    var newFileName = $"{Guid.NewGuid()}{Path.GetExtension(att.Files)}";
                    var newFilePath = Path.Combine(uploadsFolder, newFileName);

                    // Copy file
                    File.Copy(originalFilePath, newFilePath);

                    // Create new attachment entry
                    newAttachments.Add(new Attachement
                    {
                        Files = newFileName,
                        TransactionId = newTransaction.Id
                    });
                }

                if (newAttachments.Any())
                    await _context.Attachements.AddRangeAsync(newAttachments);
            }

            await _context.SaveChangesAsync();
            return newTransaction.Id;
        }


    }
}
