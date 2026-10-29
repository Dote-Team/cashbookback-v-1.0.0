using FingerprintBridge;

// البرنامج الوسيط لقارئ البصمة: يستمع على 127.0.0.1 فقط، ولا يقبل إلا طلبات الواجهة المسموحة.
// التشغيل:      CashbookFingerprintBridge.exe            (القارئ الحقيقي)
// المحاكاة:     CashbookFingerprintBridge.exe --simulate (بلا جهاز، للتطوير)
// كخدمة ويندوز: انظر README.md
bool simulate = args.Contains("--simulate");

WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
	Args = args.Where(a => a != "--simulate").ToArray(),
	ContentRootPath = AppContext.BaseDirectory
});
builder.Host.UseWindowsService(options => options.ServiceName = "CashbookFingerprintBridge");
simulate |= builder.Configuration.GetValue<bool>("Bridge:Simulate");
int port = builder.Configuration.GetValue("Bridge:Port", 52181);
string[] allowedOrigins = builder.Configuration.GetSection("Bridge:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.WebHost.ConfigureKestrel(k => k.ListenLocalhost(port));
if (simulate)
{
	builder.Services.AddSingleton<IFingerprintScanner, SimulatedScanner>();
}
else
{
	builder.Services.AddSingleton<IFingerprintScanner, ZkScanner>();
}
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().WithMethods("GET", "POST")));

WebApplication app = builder.Build();

// المتصفح يطلب إذناً خاصاً للوصول من موقع على الإنترنت إلى عنوان محلي (Private Network Access).
// ونرفض أي مصدر غير مسموح حتى خارج المتصفح، فلا تلتقط صفحةٌ غريبة بصمة المستخدم.
app.Use(async (context, next) =>
{
	string? origin = context.Request.Headers.Origin;
	if (!string.IsNullOrEmpty(origin) && !allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
	{
		context.Response.StatusCode = StatusCodes.Status403Forbidden;
		await context.Response.WriteAsync("origin not allowed");
		return;
	}
	if (context.Request.Headers.ContainsKey("Access-Control-Request-Private-Network"))
	{
		context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
	}
	await next();
});
app.UseCors();

app.MapGet("/status", (IFingerprintScanner scanner) => Results.Ok(new
{
	bridge = "cashbook-fingerprint-bridge",
	version = "1.0.0",
	scanner = scanner.Status()
}));

app.MapPost("/capture", async (IFingerprintScanner scanner, int? timeoutSeconds, string? finger, CancellationToken cancellationToken) =>
{
	TimeSpan timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds ?? 15, 3, 60));
	try
	{
		CaptureResult? result = await scanner.CaptureAsync(timeout, finger, cancellationToken);
		return result == null
			? Results.Json(new { error = "انتهت المهلة دون وضع إصبع على القارئ." }, statusCode: StatusCodes.Status408RequestTimeout)
			: Results.Ok(result);
	}
	catch (InvalidOperationException ex)
	{
		return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
	}
});

app.Logger.LogInformation("البرنامج الوسيط للبصمة يعمل على http://127.0.0.1:{Port} — {Mode}", port, simulate ? "وضع المحاكاة" : "القارئ ZK9500");
app.Run();
