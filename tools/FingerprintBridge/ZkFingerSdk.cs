using System.Runtime.InteropServices;

namespace FingerprintBridge;

/// <summary>
/// ربط مباشر بمكتبة ZKFinger SDK (libzkfp.dll) التي يثبّتها برنامج تعريف ZK9500 في
/// C:\Windows\System32. نستخدم منها الالتقاط فقط: الصورة الخام تُرسل للخادم ليطابقها.
/// </summary>
internal static class ZkFingerSdk
{
	private const string Library = "libzkfp.dll";

	public const int Ok = 0;

	public const int AlreadyInitialized = 1;

	public const int ParamWidth = 1;

	public const int ParamHeight = 2;

	public const int ParamDpi = 3;

	[DllImport(Library, CallingConvention = CallingConvention.StdCall)]
	public static extern int ZKFPM_Init();

	[DllImport(Library, CallingConvention = CallingConvention.StdCall)]
	public static extern int ZKFPM_Terminate();

	[DllImport(Library, CallingConvention = CallingConvention.StdCall)]
	public static extern int ZKFPM_GetDeviceCount();

	[DllImport(Library, CallingConvention = CallingConvention.StdCall)]
	public static extern IntPtr ZKFPM_OpenDevice(int index);

	[DllImport(Library, CallingConvention = CallingConvention.StdCall)]
	public static extern int ZKFPM_CloseDevice(IntPtr handle);

	[DllImport(Library, CallingConvention = CallingConvention.StdCall)]
	public static extern int ZKFPM_GetParameters(IntPtr handle, int code, byte[] value, ref int size);

	/// <summary>يعيد 0 عند وجود إصبع على القارئ وامتلاء المخزن بالصورة، وقيمة سالبة إن لم يوجد إصبع بعد.</summary>
	[DllImport(Library, CallingConvention = CallingConvention.StdCall)]
	public static extern int ZKFPM_AcquireFingerprintImage(IntPtr handle, byte[] image, int size);
}
