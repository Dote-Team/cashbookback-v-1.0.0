using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;

namespace cashbook.Services;

/// <summary>ما يحتاجه وسيط الجلسة من صف الجلسة، بلا الرموز نفسها.</summary>
public sealed record CachedSession(Guid SessionId, Guid UserId, DateTime ExpiredAt, string? DeviceToken, bool UserActive);

public interface ISessionCache
{
	CachedSession? Get(Guid sessionId, Guid userId);

	void Set(CachedSession session);

	/// <summary>بعد تسجيل الخروج أو تدوير رمز التحديث.</summary>
	void InvalidateSession(Guid sessionId);

	/// <summary>بعد تعطيل المستخدم أو تغيير كلمة مروره: كل جلساته تُقرأ من قاعدة البيانات مجدداً.</summary>
	void InvalidateUsers(IEnumerable<Guid> userIds);
}

/// <summary>
/// كان وسيط الجلسة يستعلم قاعدة البيانات في كل طلب. الآن تُحفظ نتيجة الاستعلام في الذاكرة
/// مدة قصيرة (30 ثانية افتراضياً). كل ما يُنهي جلسة يُبطل نسختها فوراً، فلا ينتظر المعطَّل
/// انتهاء المدة. على خادم واحد يكفي هذا؛ مع عدة خوادم تبقى المدة القصيرة هي الحد الأقصى للتأخير.
/// </summary>
public sealed class SessionCache : ISessionCache
{
	private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

	private readonly IMemoryCache _cache;

	// رقم جيل لكل مستخدم: إبطاله يرفع الرقم، فتُرفض كل نسخه المخزنة دون تتبع مفاتيحها.
	private readonly ConcurrentDictionary<Guid, long> _userGenerations = new ConcurrentDictionary<Guid, long>();

	private sealed record Entry(CachedSession Session, long Generation);

	// ذاكرة خاصة بحد أقصى للعدد، فلا تنمو بلا سقف مهما كثرت الجلسات.
	public SessionCache()
	{
		_cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100_000 });
	}

	public CachedSession? Get(Guid sessionId, Guid userId)
	{
		if (_cache.TryGetValue(Key(sessionId), out Entry? entry) && entry != null
			&& entry.Session.UserId == userId
			&& entry.Generation == Generation(userId))
		{
			return entry.Session;
		}
		return null;
	}

	public void Set(CachedSession session)
	{
		_cache.Set(Key(session.SessionId), new Entry(session, Generation(session.UserId)), new MemoryCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = Lifetime,
			Size = 1
		});
	}

	public void InvalidateSession(Guid sessionId) => _cache.Remove(Key(sessionId));

	public void InvalidateUsers(IEnumerable<Guid> userIds)
	{
		foreach (Guid userId in userIds)
		{
			_userGenerations.AddOrUpdate(userId, 1, (_, current) => current + 1);
		}
	}

	private long Generation(Guid userId) => _userGenerations.TryGetValue(userId, out long generation) ? generation : 0;

	private static string Key(Guid sessionId) => "session:" + sessionId.ToString("N");
}
