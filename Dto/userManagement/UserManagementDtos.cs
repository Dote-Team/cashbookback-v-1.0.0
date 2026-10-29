using System;
using System.Collections.Generic;

namespace cashbook.Dto.userManagement;

public class BookGrantDto
{
	/// <summary>معرّف الخزنة.</summary>
	public Guid BookId { get; set; }

	/// <summary>رموز الصلاحيات على هذه الخزنة، مثل book.view و transaction.create (القائمة الكاملة من /API/Permission/catalog).</summary>
	public List<string> Permissions { get; set; } = new List<string>();
}

public class CreateManagedUserDto
{
	/// <summary>اسم الدخول (فريد).</summary>
	public string Username { get; set; } = string.Empty;

	/// <summary>الاسم الظاهر.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>البريد (اختياري).</summary>
	public string? Email { get; set; }

	/// <summary>كلمة المرور: 8 أحرف على الأقل، فيها حرف ورقم.</summary>
	public string Password { get; set; } = string.Empty;

	/// <summary>الخزائن وصلاحيات كل منها.</summary>
	public List<BookGrantDto> Grants { get; set; } = new List<BookGrantDto>();
}

public class UpdateGrantsDto
{
	/// <summary>الخزائن وصلاحيات كل منها.</summary>
	public List<BookGrantDto> Grants { get; set; } = new List<BookGrantDto>();
}

public class UpdateManagedUserDto
{
	/// <summary>الاسم الظاهر.</summary>
	public string? Name { get; set; }

	/// <summary>البريد (اختياري).</summary>
	public string? Email { get; set; }
}

public class ResetPasswordDto
{
	/// <summary>كلمة المرور: 8 أحرف على الأقل، فيها حرف ورقم.</summary>
	public string Password { get; set; } = string.Empty;
}

public class BookGrantViewDto
{
	/// <summary>معرّف الخزنة.</summary>
	public Guid BookId { get; set; }

	public string BookName { get; set; } = string.Empty;

	public Guid BusinessId { get; set; }

	public string BusinessName { get; set; } = string.Empty;

	/// <summary>رموز الصلاحيات على هذه الخزنة، مثل book.view و transaction.create (القائمة الكاملة من /API/Permission/catalog).</summary>
	public List<string> Permissions { get; set; } = new List<string>();
}

public class ManagedUserDto
{
	public Guid Id { get; set; }

	/// <summary>اسم الدخول (فريد).</summary>
	public string Username { get; set; } = string.Empty;

	/// <summary>الاسم الظاهر.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>البريد (اختياري).</summary>
	public string Email { get; set; } = string.Empty;

	public int Level { get; set; }

	public string LevelName { get; set; } = string.Empty;

	public bool IsActive { get; set; }

	public Guid? ParentUserId { get; set; }

	public string? ParentName { get; set; }

	public DateTime CreatedAt { get; set; }

	public int FingerprintCount { get; set; }

	/// <summary>الخزائن وصلاحيات كل منها.</summary>
	public List<BookGrantViewDto> Grants { get; set; } = new List<BookGrantViewDto>();
}

public class PermissionDefinitionDto
{
	public string Code { get; set; } = string.Empty;

	/// <summary>الاسم الظاهر.</summary>
	public string Name { get; set; } = string.Empty;

	public string Group { get; set; } = string.Empty;

	public bool SuperAdminGrantOnly { get; set; }
}

public class MyPermissionsDto
{
	public Guid UserId { get; set; }

	public int Level { get; set; }

	public string LevelName { get; set; } = string.Empty;

	public bool IsSuperAdmin { get; set; }

	/// <summary>الصلاحيات لكل خزنة (معرّف الخزنة ← رموز الصلاحيات).</summary>
	public Dictionary<Guid, List<string>> Books { get; set; } = new Dictionary<Guid, List<string>>();

	/// <summary>اتحاد الصلاحيات على كل خزائن المنشأة المطلوبة؛ لإظهار أزرار مستوى المنشأة.</summary>
	public List<string> Business { get; set; } = new List<string>();

	public bool CanManageUsers { get; set; }
}
