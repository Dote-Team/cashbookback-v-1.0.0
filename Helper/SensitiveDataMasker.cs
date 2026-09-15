using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace cashbook.Helper;

public static class SensitiveDataMasker
{
	public const string Mask = "***";

	private static readonly string[] SensitiveFragments = new string[15]
	{
		"password", "passwd", "passphrase", "token", "secret", "apikey", "apisecret", "authorization", "credential", "connectionstring",
		"privatekey", "symmetrickey", "cardnumber", "iban", "cvv"
	};

	private static readonly string[] SensitiveExactNames = new string[5] { "pwd", "pin", "otp", "seed", "key" };

	public static bool IsSensitiveName(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return false;
		}
		string text = Normalize(name);
		if (SensitiveExactNames.Contains<string>(text, StringComparer.Ordinal))
		{
			return true;
		}
		string[] sensitiveFragments = SensitiveFragments;
		foreach (string value in sensitiveFragments)
		{
			if (text.Contains(value, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	private static string Normalize(string name)
	{
		return name.Replace("_", string.Empty).Replace("-", string.Empty).Replace(".", string.Empty)
			.Replace(":", string.Empty)
			.Replace(" ", string.Empty)
			.ToLowerInvariant();
	}

	public static string? MaskJson(string? json, int maxLength = 8000)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return null;
		}
		if (json.Length > maxLength * 4)
		{
			return Truncate($"{{\"note\":\"جسم الطلب كبير جدا\u064b ({json.Length} حرف) ولم ي\u064fسج\u064e\u0651ل\"}}", maxLength);
		}
		try
		{
			JToken jToken = JToken.Parse(json);
			MaskToken(jToken);
			return Truncate(jToken.ToString(Formatting.None), maxLength);
		}
		catch (JsonReaderException)
		{
			return "{\"note\":\"جسم الطلب ليس JSON صالحا\u064b — لم ي\u064fسج\u064e\u0651ل\"}";
		}
	}

	private static void MaskToken(JToken token)
	{
		if (!(token is JObject jObject))
		{
			if (!(token is JArray jArray))
			{
				return;
			}
			{
				foreach (JToken item in jArray)
				{
					MaskToken(item);
				}
				return;
			}
		}
		foreach (JProperty item2 in jObject.Properties().ToList())
		{
			if (IsSensitiveName(item2.Name))
			{
				item2.Value = "***";
			}
			else
			{
				MaskToken(item2.Value);
			}
		}
	}

	public static string? MaskQueryString(string? queryString)
	{
		if (string.IsNullOrWhiteSpace(queryString))
		{
			return null;
		}
		string text = (queryString.StartsWith('?') ? queryString.Substring(1) : queryString);
		if (text.Length == 0)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = true;
		string[] array = text.Split('&', StringSplitOptions.RemoveEmptyEntries);
		foreach (string text2 in array)
		{
			if (!flag)
			{
				stringBuilder.Append('&');
			}
			flag = false;
			int num = text2.IndexOf('=');
			if (num < 0)
			{
				stringBuilder.Append(text2);
				continue;
			}
			string value = text2.Substring(0, num);
			string text3 = text2.Substring(num + 1);
			stringBuilder.Append(value);
			stringBuilder.Append('=');
			stringBuilder.Append(IsSensitiveName(SafeUrlDecode(value)) ? "***" : text3);
		}
		string text4 = stringBuilder.ToString();
		return (text4.Length == 0) ? null : Truncate(text4, 2000);
	}

	public static string? MaskValue(string? fieldName, string? value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}
		return IsSensitiveName(fieldName) ? "***" : value;
	}

	private static string SafeUrlDecode(string value)
	{
		try
		{
			return Uri.UnescapeDataString(value.Replace('+', ' '));
		}
		catch (UriFormatException)
		{
			return value;
		}
	}

	private static string Truncate(string value, int maxLength)
	{
		return (value.Length <= maxLength) ? value : (value.Substring(0, maxLength) + "…(مقطوع)");
	}

	public static string? ToInvariantString(object? value)
	{
		if (1 == 0)
		{
		}
		string result = ((value == null) ? null : ((value is decimal num) ? num.ToString(CultureInfo.InvariantCulture) : ((value is double num2) ? num2.ToString(CultureInfo.InvariantCulture) : ((value is float num3) ? num3.ToString(CultureInfo.InvariantCulture) : ((value is DateTime dateTime) ? dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : ((value is DateTimeOffset dateTimeOffset) ? dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : ((!(value is byte[] array)) ? value.ToString() : $"<{array.Length} bytes>")))))));
		if (1 == 0)
		{
		}
		return result;
	}
}
