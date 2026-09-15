using System;
using System.Collections.Generic;
using System.Linq;

namespace cashbook.Helper;

public static class Roles
{
	public const string Owner = "owner";

	public const string Partner = "partner";

	public const string Viewer = "viewer";

	public const string Admin = "admin";

	public const string Staff = "staff";

	public const string DataOperator = "dataoperator";

	public const string PrivateViewer = "privateviewer";

	public const string PortfolioManager = "portfolio_manager";

	public const string SuperAdmin = "superadmin";

	public static readonly string[] BookScoped = new string[5] { "staff", "dataoperator", "admin", "privateviewer", "portfolio_manager" };

	public static readonly string[] Privileged = new string[3] { "owner", "partner", "viewer" };

	public static readonly string[] Management = new string[2] { "owner", "partner" };

	public static readonly string[] Assignable = new string[8] { "owner", "partner", "viewer", "admin", "staff", "dataoperator", "privateviewer", "portfolio_manager" };

	public static readonly string[] AnyMember = new string[8] { "owner", "partner", "viewer", "admin", "staff", "dataoperator", "privateviewer", "portfolio_manager" };

	public static readonly string[] TransactionRead = new string[7] { "owner", "partner", "viewer", "admin", "staff", "dataoperator", "privateviewer" };

	public static readonly string[] TransactionDuplicate = new string[7] { "owner", "partner", "admin", "staff", "dataoperator", "privateviewer", "portfolio_manager" };

	public static readonly string[] Writers = new string[5] { "owner", "partner", "admin", "staff", "dataoperator" };

	public static readonly string[] MemberRead = new string[3] { "owner", "partner", "viewer" };

	public static readonly string[] OwnerOnly = new string[1] { "owner" };

	public static bool IsBookScoped(string? role)
	{
		return role != null && Enumerable.Contains(BookScoped, role.ToLowerInvariant());
	}

	public static bool IsPrivileged(string? role)
	{
		return role != null && Enumerable.Contains(Privileged, role.ToLowerInvariant());
	}

	public static bool CanManageAllExchangeRates(string? role)
	{
		return role != null && string.Equals(role, "superadmin", StringComparison.OrdinalIgnoreCase);
	}

	public static bool CanManageExchangeRate(string? role, IEnumerable<Guid>? bookIds, Guid bookId)
	{
		if (CanManageAllExchangeRates(role))
		{
			return true;
		}
		if (role == null)
		{
			return false;
		}
		if (!string.Equals(role, "portfolio_manager", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		return bookIds?.Contains(bookId) ?? false;
	}
}
