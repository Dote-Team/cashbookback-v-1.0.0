using System;
using System.Collections.Generic;

namespace cashbook.Dto.userManagement;

public class BookGrantDto
{
	public Guid BookId { get; set; }

	public List<string> Permissions { get; set; } = new List<string>();
}

public class CreateManagedUserDto
{
	public string Username { get; set; }

	public string Name { get; set; }

	public string? Email { get; set; }

	public string Password { get; set; }

	public List<BookGrantDto> Grants { get; set; } = new List<BookGrantDto>();
}

public class UpdateGrantsDto
{
	public List<BookGrantDto> Grants { get; set; } = new List<BookGrantDto>();
}

public class UpdateManagedUserDto
{
	public string? Name { get; set; }

	public string? Email { get; set; }
}

public class ResetPasswordDto
{
	public string Password { get; set; }
}

public class BookGrantViewDto
{
	public Guid BookId { get; set; }

	public string BookName { get; set; }

	public Guid BusinessId { get; set; }

	public string BusinessName { get; set; }

	public List<string> Permissions { get; set; } = new List<string>();
}

public class ManagedUserDto
{
	public Guid Id { get; set; }

	public string Username { get; set; }

	public string Name { get; set; }

	public string Email { get; set; }

	public int Level { get; set; }

	public string LevelName { get; set; }

	public bool IsActive { get; set; }

	public Guid? ParentUserId { get; set; }

	public string? ParentName { get; set; }

	public DateTime CreatedAt { get; set; }

	public int FingerprintCount { get; set; }

	public List<BookGrantViewDto> Grants { get; set; } = new List<BookGrantViewDto>();
}

public class PermissionDefinitionDto
{
	public string Code { get; set; }

	public string Name { get; set; }

	public string Group { get; set; }

	public bool SuperAdminGrantOnly { get; set; }
}

public class MyPermissionsDto
{
	public Guid UserId { get; set; }

	public int Level { get; set; }

	public string LevelName { get; set; }

	public bool IsSuperAdmin { get; set; }

	/// <summary>الصلاحيات لكل خزنة (معرّف الخزنة ← رموز الصلاحيات).</summary>
	public Dictionary<Guid, List<string>> Books { get; set; } = new Dictionary<Guid, List<string>>();

	/// <summary>اتحاد الصلاحيات على كل خزائن المنشأة المطلوبة؛ لإظهار أزرار مستوى المنشأة.</summary>
	public List<string> Business { get; set; } = new List<string>();

	public bool CanManageUsers { get; set; }
}
