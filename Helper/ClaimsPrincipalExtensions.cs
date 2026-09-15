using System;
using System.Security.Claims;

namespace cashbook.Helper;

public static class ClaimsPrincipalExtensions
{
	public static Guid? GetUserId(this ClaimsPrincipal? principal)
	{
		string input = principal?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
		Guid result;
		return (Guid.TryParse(input, out result) && result != Guid.Empty) ? new Guid?(result) : ((Guid?)null);
	}
}
