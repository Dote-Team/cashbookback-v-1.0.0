using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using cashbook.Data;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
	private readonly ApplicationDbContext _db;

	internal DbSet<T> dbSet;

	public Repository(ApplicationDbContext db)
	{
		_db = db;
		dbSet = _db.Set<T>();
	}

	public async Task CreateAsync(T entity)
	{
		await dbSet.AddAsync(entity);
		await SaveAsync();
	}

	public async Task<T?> GetAsync(Expression<Func<T, bool>>? filter = null, bool tracked = true)
	{
		IQueryable<T> query = dbSet;
		if (!tracked)
		{
			query = query.AsNoTracking();
		}
		if (filter != null)
		{
			query = query.Where(filter);
		}
		return await query.FirstOrDefaultAsync();
	}

	public async Task<List<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null)
	{
		IQueryable<T> query = dbSet;
		if (filter != null)
		{
			query = query.Where(filter);
		}
		return await query.ToListAsync();
	}

	public async Task RemoveAsync(T entity)
	{
		dbSet.Remove(entity);
		await SaveAsync();
	}

	public async Task ArchiveAsync(T entity, Guid? archivedByUserId, string? reason = null)
	{
		if (entity is not IArchivable archivable)
		{
			throw new InvalidOperationException(typeof(T).Name + " لا يدعم الأرشفة.");
		}
		archivable.IsArchived = true;
		archivable.ArchivedAt = DateTime.UtcNow;
		archivable.ArchivedByUserId = archivedByUserId;
		archivable.ArchiveReason = ArchiveText.Reason(reason);
		await SaveAsync();
	}

	public async Task<string?> GetUserRoleAsync(Guid userId, Guid businessId)
	{
		return await (from bu in _db.BusinessUsers
			where bu.UserId == userId && bu.BusinessId == businessId
			select bu.Role.ToLower()).FirstOrDefaultAsync();
	}

	public async Task SaveAsync()
	{
		await _db.SaveChangesAsync();
	}

	public async Task UpdateAsync(T entity)
	{
		dbSet.Update(entity);
		await SaveAsync();
	}

	public async Task<List<T>> GetPaginatedAsync(int? skip = null, int? take = null, Expression<Func<T, bool>>? filter = null)
	{
		int currentPage = skip ?? 1;
		int currentSize = take ?? 25;
		IQueryable<T> query = dbSet;
		if (filter != null)
		{
			query = query.Where(filter);
		}
		query = query.Skip((currentPage - 1) * currentSize).Take(currentSize);
		return await query.ToListAsync();
	}

	public async Task<int> GetCountAsync(Expression<Func<T, bool>>? filter = null)
	{
		IQueryable<T> query = dbSet;
		if (filter != null)
		{
			query = query.Where(filter);
		}
		return await query.CountAsync();
	}
}
