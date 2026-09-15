namespace cashbook.Services;

public interface ILoginThrottle
{
	bool IsBlocked(string key, out int retryAfterSeconds);

	void RecordFailure(string key);

	void Reset(string key);
}
