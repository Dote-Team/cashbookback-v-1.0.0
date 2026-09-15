using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using cashbook.Models.Constants;

namespace cashbook.Helper;

public static class AuditChangeSet
{
	private const int MaxFieldsInSummary = 6;

	public static string? ToJson(IReadOnlyList<AuditFieldChange> changes)
	{
		if (changes.Count == 0)
		{
			return null;
		}
		JObject jObject = new JObject();
		jObject["changes"] = new JArray(changes.Select((AuditFieldChange change) => new JObject
		{
			["field"] = change.Field,
			["old"] = change.OldValue,
			["new"] = change.NewValue
		}));
		return jObject.ToString(Formatting.None);
	}

	public static string Build(string? entityName, string? operation, IReadOnlyList<AuditFieldChange> changes)
	{
		string text = AuditNarrator.LabelEntity(entityName);
		string text2 = AuditOperation.Verb(operation);
		if (changes.Count == 0)
		{
			return text2 + " " + text;
		}
		List<string> list = new List<string>(Math.Min(changes.Count, 6));
		foreach (AuditFieldChange item2 in changes.Take(6))
		{
			string text3 = AuditNarrator.LabelField(item2.Field);
			string text4 = AuditNarrator.FormatValue(item2.OldValue);
			string text5 = AuditNarrator.FormatValue(item2.NewValue);
			List<string> list2 = list;
			if (1 == 0)
			{
			}
			string item = ((operation == "Added") ? (text3 + ": " + text5) : ((!(operation == "Deleted")) ? ((text4 == text5) ? (text3 + ": " + text5) : $"{text3}: {text4} ← {text5}") : (text3 + ": " + text4)));
			if (1 == 0)
			{
			}
			list2.Add(item);
		}
		string text6 = string.Join("، ", list);
		if (changes.Count > 6)
		{
			text6 += $"، و{changes.Count - 6} حقل آخر";
		}
		return $"{text2} {text} — {text6}";
	}
}
