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
		T result = await query.FirstOrDefaultAsync();
		if (result == null)
		{
			return null;
		}
		PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
		PropertyInfo[] array = properties;
		foreach (PropertyInfo property in array)
		{
			if (!(property.PropertyType == typeof(string)))
			{
				continue;
			}
			string value = property.GetValue(result) as string;
			if (!string.IsNullOrEmpty(value) && (value.Trim().StartsWith("{") || value.Trim().StartsWith("[")))
			{
				try
				{
					property.SetValue(result, JsonConvert.DeserializeObject(value)?.ToString());
				}
				catch (JsonReaderException)
				{
				}
			}
		}
		return result;
	}

	public async Task<List<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null)
	{
		IQueryable<T> query = dbSet;
		if (filter != null)
		{
			query = query.Where(filter);
		}
		List<T> results = await query.ToListAsync();
		foreach (T item in results)
		{
			PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
			PropertyInfo[] array = properties;
			foreach (PropertyInfo property in array)
			{
				if (!(property.PropertyType == typeof(string)))
				{
					continue;
				}
				string value = property.GetValue(item) as string;
				if (!string.IsNullOrEmpty(value) && (value.Trim().StartsWith("{") || value.Trim().StartsWith("[")))
				{
					try
					{
						object deserializedValue = JsonConvert.DeserializeObject(value);
						property.SetValue(item, deserializedValue);
					}
					catch (JsonReaderException)
					{
					}
				}
			}
		}
		return results;
	}

	public async Task RemoveAsync(T entity)
	{
		dbSet.Remove(entity);
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

	public async Task<List<string>> UploadFilesAsync(IEnumerable<IFormFile> files, string uploadPath)
	{
		if (files == null || !files.Any())
		{
			throw new ArgumentException("No files provided for upload.");
		}
		List<string> fileNames = new List<string>();
		try
		{
			foreach (IFormFile file in files)
			{
				if (file.Length > 0)
				{
					if (!Directory.Exists(uploadPath))
					{
						Directory.CreateDirectory(uploadPath);
					}
					string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
					string filePath = Path.Combine(uploadPath, uniqueFileName);
					using (FileStream stream = new FileStream(filePath, FileMode.Create))
					{
						await file.CopyToAsync(stream);
					}
					fileNames.Add(uniqueFileName);
				}
			}
			return fileNames;
		}
		catch (Exception innerException)
		{
			foreach (string fileName in fileNames)
			{
				string filePath2 = Path.Combine(uploadPath, fileName);
				if (File.Exists(filePath2))
				{
					File.Delete(filePath2);
				}
			}
			throw new InvalidOperationException("Error occurred while uploading files", innerException);
		}
	}
}
