using System.Collections.Generic;

namespace cashbook.Dto;

public class PaginatedResponse<T>
{
	public int TotalRecords { get; set; }

	public int Skip { get; set; }

	public int Take { get; set; }

	public IEnumerable<T> Data { get; set; }
}
