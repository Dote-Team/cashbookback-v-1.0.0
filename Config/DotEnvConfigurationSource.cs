using Microsoft.Extensions.Configuration;

namespace cashbook.Config;

internal sealed class DotEnvConfigurationSource : IConfigurationSource
{
	private readonly string _path;

	public DotEnvConfigurationSource(string path)
	{
		_path = path;
	}

	public IConfigurationProvider Build(IConfigurationBuilder builder)
	{
		return new DotEnvConfigurationProvider(_path);
	}
}
