namespace cashbook.Models.Constants;

public static class AuditOperation
{
	public const string Added = "Added";

	public const string Modified = "Modified";

	public const string Deleted = "Deleted";

	public const string Read = "Read";

	public static string Label(string? operation)
	{
		string result = operation switch
		{
			"Added" => "إنشاء", 
			"Modified" => "تعديل", 
			"Deleted" => "حذف", 
			"Read" => "قراءة", 
			_ => "غير محد\u0651د", 
		};
		return result;
	}

	public static string Verb(string? operation)
	{
		string result = operation switch
		{
			"Added" => "أنشأ", 
			"Modified" => "عد\u0651ل", 
			"Deleted" => "حذف", 
			"Read" => "اط\u0651لع على", 
			_ => "غي\u0651ر", 
		};
		return result;
	}
}
