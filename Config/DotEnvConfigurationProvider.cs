using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace cashbook.Config;

internal sealed class DotEnvConfigurationProvider : ConfigurationProvider
{
	private readonly string _path;

	public bool LastLoadSucceeded { get; private set; }

	public IReadOnlyCollection<string> LoadedKeys => base.Data.Keys.ToList();

	public DotEnvConfigurationProvider(string path)
	{
		_path = path;
	}

	public override void Load()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			string[] array = File.ReadAllLines(_path);
			foreach (string raw in array)
			{
				KeyValuePair<string, string>? keyValuePair = DotEnv.ParseLine(raw);
				if (keyValuePair.HasValue)
				{
					dictionary[keyValuePair.Value.Key] = keyValuePair.Value.Value;
				}
			}
			LastLoadSucceeded = true;
		}
		catch (Exception)
		{
			LastLoadSucceeded = false;
		}
		base.Data = dictionary;
	}
}
