using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace cashbook.Config;

public static class DotEnv
{
	public static IConfigurationBuilder AddDotEnvFile(this IConfigurationBuilder builder, string? path = null)
	{
		string text = Resolve(path);
		if (text != null)
		{
			builder.Add(new DotEnvConfigurationSource(text));
		}
		return builder;
	}

	internal static string? Resolve(string? explicitPath)
	{
		if (!string.IsNullOrWhiteSpace(explicitPath))
		{
			return File.Exists(explicitPath) ? explicitPath : null;
		}
		string environmentVariable = Environment.GetEnvironmentVariable("DOTENV_PATH");
		if (!string.IsNullOrWhiteSpace(environmentVariable) && File.Exists(environmentVariable))
		{
			return environmentVariable;
		}
		string[] array = new string[2]
		{
			Directory.GetCurrentDirectory(),
			AppContext.BaseDirectory
		};
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				string text2 = Path.Combine(text, ".env");
				if (File.Exists(text2))
				{
					return text2;
				}
			}
		}
		return null;
	}

	internal static KeyValuePair<string, string?>? ParseLine(string raw)
	{
		string text = raw.Trim();
		if (text.Length == 0 || text[0] == '#')
		{
			return null;
		}
		if (text.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
		{
			text = text.Substring("export ".Length).TrimStart();
		}
		int num = text.IndexOf('=');
		if (num <= 0)
		{
			return null;
		}
		string text2 = text.Substring(0, num).Trim();
		if (text2.Length == 0)
		{
			return null;
		}
		string text3 = text.Substring(num + 1).Trim();
		if (text3.Length >= 2 && ((text3[0] == '"' && text3[text3.Length - 1] == '"') || (text3[0] == '\'' && text3[text3.Length - 1] == '\'')))
		{
			text3 = text3.Substring(1, text3.Length - 2);
		}
		else
		{
			int num2 = text3.IndexOf(" #", StringComparison.Ordinal);
			if (num2 >= 0)
			{
				text3 = text3.Substring(0, num2).Trim();
			}
		}
		text2 = text2.Replace("__", ":");
		return new KeyValuePair<string, string>(text2, text3);
	}
}
