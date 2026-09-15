namespace cashbook.Helper;

public static class PasswordPolicy
{
	public const int MinLength = 8;

	public const int MaxLength = 128;

	public static bool IsValid(string? password, out string error)
	{
		error = string.Empty;
		if (string.IsNullOrWhiteSpace(password))
		{
			error = "Password is required.";
			return false;
		}
		if (password.Length < 8)
		{
			error = $"Password must be at least {8} characters.";
			return false;
		}
		if (password.Length > 128)
		{
			error = $"Password must not exceed {128} characters.";
			return false;
		}
		bool flag = false;
		bool flag2 = false;
		foreach (char c in password)
		{
			if (char.IsLetter(c))
			{
				flag = true;
			}
			else if (char.IsDigit(c))
			{
				flag2 = true;
			}
		}
		if (!flag || !flag2)
		{
			error = "Password must contain at least one letter and one digit.";
			return false;
		}
		return true;
	}
}
