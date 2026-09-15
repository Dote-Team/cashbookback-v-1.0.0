using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace cashbook.Models.Enums;

[JsonConverter(typeof(StringEnumConverter))]
public enum CurrencyCode
{
	USD = 1,
	IQD
}
