using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using cashbook.Models;

namespace cashbook.Helper;

/// <summary>
/// البيانات المرتبطة بالعنصر المحذوف (صفوف تابعة تُحفظ كنسخة كي ترجع مع الاستعادة).
/// </summary>
public class TrashRelatedData
{
	public Dictionary<string, List<Dictionary<string, object>>> Rows { get; set; } = new Dictionary<string, List<Dictionary<string, object>>>();

	/// <summary>
	/// صفوف أُنشئت أثناء الحذف (مثل قيد سجل الحركة) وتُمسح عند الاستعادة.
	/// </summary>
	public Dictionary<string, List<Dictionary<string, object>>> DeleteRows { get; set; } = new Dictionary<string, List<Dictionary<string, object>>>();
}

public static class TrashHelper
{
	private static readonly HashSet<Type> ScalarTypes = new HashSet<Type>
	{
		typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal), typeof(char), typeof(DateTime), typeof(DateTimeOffset), typeof(Guid), typeof(TimeSpan)
	};

	public static TrashItem Build(object entity, Guid? deletedByUserId)
	{
		if (entity == null)
		{
			throw new ArgumentNullException(nameof(entity));
		}
		Type type = entity.GetType();
		return new TrashItem
		{
			EntityType = type.Name,
			EntityId = GetGuid(entity, "Id"),
			BusinessId = GetGuidNullable(entity, "BusinessId"),
			BookId = GetGuidNullable(entity, "BookId"),
			DeletedByUserId = deletedByUserId,
			Name = ResolveName(entity),
			DataJson = JsonConvert.SerializeObject(ToScalarDictionary(entity)),
			DeletedAt = DateTime.Now
		};
	}

	/// <summary>
	/// يحوّل خصائص الكيان القابلة للتخزين (قيَم مفردة أو قوائم قيَم مفردة) إلى قاموس يُخزَّن JSON.
	/// تُستثنى خصائص التنقّل لأنها تُحفظ في مجموعات منفصلة.
	/// </summary>
	public static Dictionary<string, object> ToScalarDictionary(object entity)
	{
		Dictionary<string, object> data = new Dictionary<string, object>();
		PropertyInfo[] properties = entity.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
		foreach (PropertyInfo prop in properties)
		{
			if (!prop.CanRead)
			{
				continue;
			}
			if (IsSnapshotProperty(prop.PropertyType))
			{
				data[prop.Name] = prop.GetValue(entity);
			}
		}
		return data;
	}

	public static void AddRows(TrashRelatedData data, string key, IEnumerable? entities)
	{
		if (entities == null)
		{
			return;
		}
		List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
		foreach (object entity in entities)
		{
			if (entity != null)
			{
				rows.Add(ToScalarDictionary(entity));
			}
		}
		if (rows.Count > 0)
		{
			data.Rows[key] = rows;
		}
	}

	public static void AddDeleteRow(TrashRelatedData data, string key, Dictionary<string, object> row)
	{
		if (!data.DeleteRows.TryGetValue(key, out List<Dictionary<string, object>> rows))
		{
			rows = new List<Dictionary<string, object>>();
			data.DeleteRows[key] = rows;
		}
		rows.Add(row);
	}

	public static string? Serialize(TrashRelatedData? data)
	{
		if (data == null || (data.Rows.Count == 0 && data.DeleteRows.Count == 0))
		{
			return null;
		}
		return JsonConvert.SerializeObject(data);
	}

	public static TrashRelatedData Deserialize(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return new TrashRelatedData();
		}
		try
		{
			return JsonConvert.DeserializeObject<TrashRelatedData>(json) ?? new TrashRelatedData();
		}
		catch (JsonException)
		{
			return new TrashRelatedData();
		}
	}

	/// <summary>
	/// يحذف ملفات المرفقات الفعلية من القرص — يُستدعى عند الحذف النهائي فقط.
	/// </summary>
	public static void DeleteAttachmentFiles(string? relatedJson)
	{
		TrashRelatedData data = Deserialize(relatedJson);
		if (!data.Rows.TryGetValue("Attachements", out List<Dictionary<string, object>>? rows))
		{
			return;
		}
		string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
		foreach (Dictionary<string, object> row in rows)
		{
			if (!row.TryGetValue("Files", out object? value) || value is not string fileName || string.IsNullOrEmpty(fileName))
			{
				continue;
			}
			string filePath = Path.Combine(uploadsFolder, fileName);
			try
			{
				if (File.Exists(filePath))
				{
					File.Delete(filePath);
				}
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}
	}

	public static bool IsSnapshotProperty(Type propertyType)
	{
		Type underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
		if (underlying == typeof(string))
		{
			return true;
		}
		if (underlying.IsEnum || ScalarTypes.Contains(underlying))
		{
			return true;
		}
		if (typeof(IEnumerable).IsAssignableFrom(underlying))
		{
			Type? elementType = GetCollectionElementType(underlying);
			if (elementType != null)
			{
				Type elementUnderlying = Nullable.GetUnderlyingType(elementType) ?? elementType;
				return elementUnderlying == typeof(string) || elementUnderlying.IsEnum || ScalarTypes.Contains(elementUnderlying);
			}
		}
		return false;
	}

	/// <summary>
	/// القيمة كما تُخزَّن في العمود: الـ enum باسمه النصي، وقائمة القيم المفردة كـ JSON، و null كـ DBNull.
	/// </summary>
	public static object ToDbValue(PropertyInfo prop, object entity)
	{
		object? value = prop.GetValue(entity);
		if (value == null)
		{
			return DBNull.Value;
		}
		Type underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
		if (underlying.IsEnum)
		{
			return value.ToString();
		}
		if (underlying != typeof(string) && typeof(IEnumerable).IsAssignableFrom(underlying))
		{
			return JsonConvert.SerializeObject(value);
		}
		return value;
	}

	private static Type? GetCollectionElementType(Type collectionType)
	{
		if (collectionType.IsArray)
		{
			return collectionType.GetElementType();
		}
		if (collectionType.IsGenericType)
		{
			Type[] arguments = collectionType.GetGenericArguments();
			if (arguments.Length == 1)
			{
				return arguments[0];
			}
		}
		return null;
	}

	private static Guid GetGuid(object entity, string name)
	{
		object? value = entity.GetType().GetProperty(name)?.GetValue(entity);
		return (value is Guid guid) ? guid : Guid.Empty;
	}

	private static Guid? GetGuidNullable(object entity, string name)
	{
		object? value = entity.GetType().GetProperty(name)?.GetValue(entity);
		return (value is Guid guid) ? new Guid?(guid) : null;
	}

	private static string ResolveName(object entity)
	{
		Type type = entity.GetType();
		string[] candidates = new string[5] { "Name", "Key", "Username", "Email", "Description" };
		foreach (string candidate in candidates)
		{
			object? value = type.GetProperty(candidate)?.GetValue(entity);
			if (value is string text && !string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
		}
		if (type.Name == "Transaction")
		{
			object? transactionType = type.GetProperty("Type")?.GetValue(entity);
			object? amount = type.GetProperty("Amount")?.GetValue(entity);
			object? currency = type.GetProperty("Currency")?.GetValue(entity);
			return $"{transactionType} {amount} {currency}";
		}
		return type.Name;
	}
}
