using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using cashbook.Data;
using cashbook.Dto.user;
using cashbook.Helper;
using cashbook.Models;

namespace cashbook.Services;

public interface ITokenService
{
	/// <summary>ينشئ جلسة جديدة ويصدر رمزَي الدخول والتحديث. يُستخدم للدخول بكلمة المرور وبالبصمة.</summary>
	Task<LoginResponseDto> IssueAsync(User user, string? deviceToken, string loginMethod);

	Task<LoginResponseDto?> RefreshAsync(string refreshToken);
}

/// <summary>
/// إصدار الرموز. لكل رمز claim اسمه token_type (access أو refresh)، والوسيط يرفض استخدام
/// رمز التحديث للدخول إلى الـ API. ورمز التحديث يُخزن بـ SHA-256 لا بـ BCrypt: BCrypt يقرأ
/// أول 72 بايت فقط، وهي في رموز JWT متطابقة تقريباً، فكان أي رمز للجلسة نفسها يمر.
/// </summary>
public sealed class TokenService : ITokenService
{
	public const string TokenTypeClaim = "token_type";

	public const string AccessTokenType = "access";

	public const string RefreshTokenType = "refresh";

	private readonly ApplicationDbContext _context;

	private readonly IExchangeRateAuthorization _exchangeRateAuthorization;

	private readonly IFileStorage _files;

	private readonly IConfiguration _configuration;

	private readonly byte[] _key;

	private readonly TimeSpan _accessTokenLifetime;

	private readonly int _refreshTokenDays;

	public TokenService(ApplicationDbContext context, IConfiguration configuration, IExchangeRateAuthorization exchangeRateAuthorization, IFileStorage files)
	{
		_context = context;
		_exchangeRateAuthorization = exchangeRateAuthorization;
		_files = files;
		_configuration = configuration;
		_key = SigningKey(configuration);
		// ExpireMinutes هو الإعداد الجديد (60 افتراضياً). ExpireHours يبقى مقروءاً لملفات .env قديمة.
		int? hours = configuration.GetValue<int?>("Jwt:ExpireHours");
		_accessTokenLifetime = TimeSpan.FromMinutes(Math.Clamp(configuration.GetValue<int?>("Jwt:ExpireMinutes") ?? (hours.HasValue ? hours.Value * 60 : 60), 5, 24 * 60));
		_refreshTokenDays = Math.Max(1, configuration.GetValue<int>("Jwt:RefreshExpireDays"));
	}

	public async Task<LoginResponseDto> IssueAsync(User user, string? deviceToken, string loginMethod)
	{
		Guid sessionId = Guid.NewGuid();
		(string access, string refresh) = CreateTokens(user, sessionId, loginMethod);
		_context.Sessions.Add(new Session
		{
			Id = sessionId,
			UserId = user.Id,
			RefreshToken = Hash(refresh),
			DeviceToken = deviceToken ?? "",
			ExpiredAt = DateTime.UtcNow.AddDays(_refreshTokenDays)
		});
		await _context.SaveChangesAsync();
		return await BuildResponseAsync(user, sessionId, access, refresh);
	}

	public async Task<LoginResponseDto?> RefreshAsync(string refreshToken)
	{
		ClaimsPrincipal principal;
		try
		{
			principal = new JwtSecurityTokenHandler().ValidateToken(refreshToken, ValidationParameters(_configuration), out SecurityToken validated);
			if (validated is not JwtSecurityToken jwt || !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}
		}
		catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
		{
			return null;
		}
		if (principal.FindFirstValue(TokenTypeClaim) != RefreshTokenType
			|| !Guid.TryParse(principal.FindFirstValue("sessionId"), out Guid sessionId)
			|| !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
		{
			return null;
		}
		User? user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
		Session? session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
		if (user == null || !user.IsActive || session == null || session.ExpiredAt < DateTime.UtcNow)
		{
			return null;
		}
		if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(refreshToken)), Encoding.ASCII.GetBytes(session.RefreshToken ?? "")))
		{
			return null;
		}
		string loginMethod = principal.FindFirstValue("login_method") ?? "pwd";
		(string access, string refresh) = CreateTokens(user, sessionId, loginMethod);
		// تدوير: رمز التحديث القديم لا يعود صالحاً بعد هذه اللحظة.
		session.RefreshToken = Hash(refresh);
		session.ExpiredAt = DateTime.UtcNow.AddDays(_refreshTokenDays);
		await _context.SaveChangesAsync();
		return await BuildResponseAsync(user, sessionId, access, refresh);
	}

	private (string Access, string Refresh) CreateTokens(User user, Guid sessionId, string loginMethod)
	{
		JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
		string Create(string type, DateTime expires)
		{
			ClaimsIdentity identity = new ClaimsIdentity(new[]
			{
				new Claim(ClaimTypes.Name, user.Username ?? user.Email),
				new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
				new Claim("sessionId", sessionId.ToString()),
				new Claim(TokenTypeClaim, type),
				new Claim("login_method", loginMethod),
				// معرّف فريد لكل رمز، فلا يتطابق رمزان أبداً حتى لو صدرا في الثانية نفسها.
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
			});
			return handler.WriteToken(handler.CreateToken(new SecurityTokenDescriptor
			{
				Subject = identity,
				Issuer = Issuer(_configuration),
				Audience = Audience(_configuration),
				Expires = expires,
				SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256)
			}));
		}
		return (Create(AccessTokenType, DateTime.UtcNow.Add(_accessTokenLifetime)), Create(RefreshTokenType, DateTime.UtcNow.AddDays(_refreshTokenDays)));
	}

	private async Task<LoginResponseDto> BuildResponseAsync(User user, Guid sessionId, string access, string refresh)
	{
		List<Guid> exchangeRateBusinesses = await _exchangeRateAuthorization.GetManageableBusinessIdsAsync(user.Id);
		int level = user.IsSuperAdmin ? UserLevels.SuperAdmin : user.Level;
		return new LoginResponseDto
		{
			Id = user.Id,
			Email = user.Email,
			Username = user.Username,
			Name = user.Name,
			ProfileImage = _files.SignedName(user.ProfileImage),
			AccessToken = access,
			RefreshToken = refresh,
			SessionId = sessionId,
			IsSuperAdmin = level == UserLevels.SuperAdmin,
			Level = level,
			LevelName = UserLevels.Name(level),
			CanManageExchangeRate = level == UserLevels.SuperAdmin || exchangeRateBusinesses.Any(),
			ExchangeRateBusinessIds = exchangeRateBusinesses
		};
	}

	public static string Issuer(IConfiguration configuration) => configuration.GetValue<string>("Jwt:Issuer") is { Length: > 0 } issuer ? issuer : "cashbook-api";

	public static string Audience(IConfiguration configuration) => configuration.GetValue<string>("Jwt:Audience") is { Length: > 0 } audience ? audience : "cashbook-client";

	/// <summary>المفتاح بترميز UTF-8: ASCII كان يحوّل أي حرف غير لاتيني في المفتاح إلى «?».</summary>
	public static byte[] SigningKey(IConfiguration configuration) => Encoding.UTF8.GetBytes(configuration.GetValue<string>("ApiSettings:Secret") ?? "");

	/// <summary>قواعد التحقق نفسها لرمز الدخول (في الوسيط) ولرمز التحديث (هنا).</summary>
	public static TokenValidationParameters ValidationParameters(IConfiguration configuration) => new TokenValidationParameters
	{
		ValidateIssuerSigningKey = true,
		IssuerSigningKey = new SymmetricSecurityKey(SigningKey(configuration)),
		ValidateIssuer = true,
		ValidIssuer = Issuer(configuration),
		ValidateAudience = true,
		ValidAudience = Audience(configuration),
		ValidateLifetime = true,
		ClockSkew = TimeSpan.Zero,
		ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
	};

	private static string Hash(string value) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
