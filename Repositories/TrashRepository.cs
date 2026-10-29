using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.Trash;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Repositories;

public class TrashRepository : ITrashRepository
{
	private readonly ApplicationDbContext _db;

	private static readonly Dictionary<string, Type> EntityTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
	{
		["Book"] = typeof(Book),
		["Business"] = typeof(Business),
		["Category"] = typeof(Category),
		["Contact"] = typeof(Contact),
		["PaymentMethod"] = typeof(PaymentMethod),
		["CustomField"] = typeof(CustomField),
		["Transaction"] = typeof(Transaction),
		["BusinessUser"] = typeof(BusinessUser)
	};

	private static readonly Dictionary<string, string> TableNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["Book"] = "Books",
		["Business"] = "Businesses",
		["Category"] = "Categories",
		["Contact"] = "Contacts",
		["PaymentMethod"] = "PaymentMethods",
		["CustomField"] = "CustomFields",
		["Transaction"] = "Transactions",
		["BusinessUser"] = "BusinessUsers"
	};

	/// <summary>
	/// ترتيب الإدراج عند الاستعادة: الأصل قبل الفرع حتى لا تُخالف قيود المفاتيح الأجنبية.
	/// </summary>
	private static readonly (string Key, string Table, Type Type)[] InsertOrder = new (string, string, Type)[]
	{
		("Businesses", "Businesses", typeof(Business)),
		("BusinessUsers", "BusinessUsers", typeof(BusinessUser)),
		("Categories", "Categories", typeof(Category)),
		("Contacts", "Contacts", typeof(Contact)),
		("PaymentMethods", "PaymentMethods", typeof(PaymentMethod)),
		("Books", "Books", typeof(Book)),
		("CustomFields", "CustomFields", typeof(CustomField)),
		("Settings", "Settings", typeof(Setting)),
		("Transactions", "Transactions", typeof(Transaction)),
		("Attachements", "Attachements", typeof(Attachement)),
		("CustomFieldValues", "CustomFieldValues", typeof(CustomFieldValue))
	};

	private static readonly Dictionary<string, string> DeleteRowTables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["TransactionHistories"] = "TransactionHistories"
	};

	public TrashRepository(ApplicationDbContext db)
	{
		_db = db;
	}

	public async Task<PaginatedResponse<TrashItemDto>> GetTrashAsync(Guid? businessId, Guid? deletedByUserId, int? skip = 1, int? take = 25, string? search = null)
	{
		int currentPage = Math.Max(1, skip ?? 1);
		int currentSize = take ?? 25;
		IQueryable<TrashItem> query = _db.TrashItems.AsQueryable();
		if (deletedByUserId.HasValue)
		{
			query = query.Where((TrashItem t) => t.DeletedByUserId == deletedByUserId.Value);
		}
		if (businessId.HasValue)
		{
			query = query.Where((TrashItem t) => t.BusinessId == businessId.Value);
		}
		if (!string.IsNullOrWhiteSpace(search))
		{
			string term = search.Trim();
			query = query.Where((TrashItem t) => t.Name != null && t.Name.Contains(term));
		}
		int totalRecords = await query.CountAsync();
		List<TrashItemDto> data = await query.OrderByDescending((TrashItem t) => t.DeletedAt).Skip((currentPage - 1) * currentSize).Take(currentSize).Select((TrashItem t) => new TrashItemDto
		{
			Id = t.Id,
			EntityType = t.EntityType,
			EntityId = t.EntityId,
			Name = t.Name,
			BusinessId = t.BusinessId,
			BookId = t.BookId,
			DeletedAt = t.DeletedAt
		}).ToListAsync();
		return new PaginatedResponse<TrashItemDto>
		{
			TotalRecords = totalRecords,
			Skip = currentPage,
			Take = currentSize,
			Data = data
		};
	}

	public async Task<TrashItem?> GetTrashItemAsync(Guid trashId)
	{
		return await _db.TrashItems.FirstOrDefaultAsync((TrashItem t) => t.Id == trashId);
	}
}
