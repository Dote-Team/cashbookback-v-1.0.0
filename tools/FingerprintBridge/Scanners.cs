using System.Security.Cryptography;
using System.Text;

namespace FingerprintBridge;

public sealed record CaptureResult(int Width, int Height, int Dpi, string Pixels);

public sealed record ScannerStatus(bool Connected, int DeviceCount, bool Simulated, int Width, int Height, int Dpi, string? Error);

public interface IFingerprintScanner
{
	ScannerStatus Status();

	/// <summary>ينتظر وضع الإصبع حتى انقضاء المهلة، ثم يعيد الصورة الخام. null عند انتهاء المهلة.</summary>
	Task<CaptureResult?> CaptureAsync(TimeSpan timeout, string? simulatedFinger, CancellationToken cancellationToken);
}

/// <summary>
/// القارئ الحقيقي ZK9500. يُفتح عند أول طلب ويُعاد فتحه تلقائياً إن فُصل وأُعيد توصيله.
/// الالتقاط مسلسل: طلب واحد في كل مرة، فالجهاز لا يخدم طلبين معاً.
/// </summary>
public sealed class ZkScanner : IFingerprintScanner, IDisposable
{
	private readonly SemaphoreSlim _lock = new(1, 1);

	private readonly ILogger<ZkScanner> _logger;

	private IntPtr _device = IntPtr.Zero;

	private bool _initialized;

	private int _width;

	private int _height;

	private int _dpi = 500;

	private string? _lastError;

	public ZkScanner(ILogger<ZkScanner> logger)
	{
		_logger = logger;
	}

	public ScannerStatus Status()
	{
		_lock.Wait();
		try
		{
			int count = EnsureOpen() ? Math.Max(1, SafeDeviceCount()) : SafeDeviceCount();
			return new ScannerStatus(_device != IntPtr.Zero, count, false, _width, _height, _dpi, _device == IntPtr.Zero ? _lastError : null);
		}
		finally
		{
			_lock.Release();
		}
	}

	public async Task<CaptureResult?> CaptureAsync(TimeSpan timeout, string? simulatedFinger, CancellationToken cancellationToken)
	{
		await _lock.WaitAsync(cancellationToken);
		try
		{
			if (!EnsureOpen())
			{
				throw new InvalidOperationException(_lastError ?? "القارئ غير متصل.");
			}
			byte[] image = new byte[_width * _height];
			DateTime deadline = DateTime.UtcNow + timeout;
			while (DateTime.UtcNow < deadline)
			{
				cancellationToken.ThrowIfCancellationRequested();
				int result = ZkFingerSdk.ZKFPM_AcquireFingerprintImage(_device, image, image.Length);
				if (result == ZkFingerSdk.Ok)
				{
					return new CaptureResult(_width, _height, _dpi, Convert.ToBase64String(image));
				}
				await Task.Delay(120, cancellationToken);
			}
			return null;
		}
		catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
		{
			Close();
			throw new InvalidOperationException("مكتبة ZKFinger SDK غير مثبتة أو غير متوافقة (64-bit): " + ex.Message, ex);
		}
		finally
		{
			_lock.Release();
		}
	}

	private bool EnsureOpen()
	{
		if (_device != IntPtr.Zero)
		{
			return true;
		}
		try
		{
			if (!_initialized)
			{
				int init = ZkFingerSdk.ZKFPM_Init();
				if (init != ZkFingerSdk.Ok && init != ZkFingerSdk.AlreadyInitialized)
				{
					_lastError = $"تعذّر تهيئة ZKFinger SDK (الرمز {init}). تأكد من توصيل القارئ وتثبيت تعريفه.";
					return false;
				}
				_initialized = true;
			}
			if (ZkFingerSdk.ZKFPM_GetDeviceCount() <= 0)
			{
				_lastError = "لا يوجد قارئ بصمة متصل.";
				return false;
			}
			_device = ZkFingerSdk.ZKFPM_OpenDevice(0);
			if (_device == IntPtr.Zero)
			{
				_lastError = "تعذّر فتح القارئ. قد يكون مستخدماً من برنامج آخر.";
				return false;
			}
			_width = ReadParameter(ZkFingerSdk.ParamWidth, 300);
			_height = ReadParameter(ZkFingerSdk.ParamHeight, 400);
			_dpi = ReadParameter(ZkFingerSdk.ParamDpi, 500);
			_lastError = null;
			_logger.LogInformation("تم فتح القارئ: {Width}x{Height} بدقة {Dpi}", _width, _height, _dpi);
			return true;
		}
		catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
		{
			_lastError = "مكتبة libzkfp.dll غير موجودة. ثبّت تعريف ZK9500 (ZKFinger SDK) ثم أعد تشغيل البرنامج.";
			return false;
		}
	}

	private int ReadParameter(int code, int fallback)
	{
		byte[] value = new byte[4];
		int size = 4;
		int result = ZkFingerSdk.ZKFPM_GetParameters(_device, code, value, ref size);
		int read = BitConverter.ToInt32(value, 0);
		return result == ZkFingerSdk.Ok && read > 0 ? read : fallback;
	}

	private int SafeDeviceCount()
	{
		try
		{
			return _initialized ? ZkFingerSdk.ZKFPM_GetDeviceCount() : 0;
		}
		catch
		{
			return 0;
		}
	}

	private void Close()
	{
		if (_device != IntPtr.Zero)
		{
			try
			{
				ZkFingerSdk.ZKFPM_CloseDevice(_device);
			}
			catch
			{
			}
			_device = IntPtr.Zero;
		}
	}

	public void Dispose()
	{
		Close();
		if (_initialized)
		{
			try
			{
				ZkFingerSdk.ZKFPM_Terminate();
			}
			catch
			{
			}
		}
	}
}

/// <summary>
/// وضع المحاكاة للتطوير قبل توفر الجهاز: يعيد صورة ثابتة لكل «إصبع» يُسمّى بالمعامل finger،
/// فتمر الدورة كاملة (تسجيل ثم دخول) مع محرّك الخادم التجريبي Fingerprint__Matcher=dev.
/// </summary>
public sealed class SimulatedScanner : IFingerprintScanner
{
	private const int Width = 300;

	private const int Height = 400;

	public ScannerStatus Status() => new(true, 1, true, Width, Height, 500, null);

	public async Task<CaptureResult?> CaptureAsync(TimeSpan timeout, string? simulatedFinger, CancellationToken cancellationToken)
	{
		await Task.Delay(600, cancellationToken);
		byte[] seed = SHA256.HashData(Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(simulatedFinger) ? "default" : simulatedFinger));
		byte[] pixels = new byte[Width * Height];
		for (int i = 0; i < pixels.Length; i++)
		{
			pixels[i] = (byte)(seed[i % seed.Length] ^ (i * 31));
		}
		return new CaptureResult(Width, Height, 500, Convert.ToBase64String(pixels));
	}
}
