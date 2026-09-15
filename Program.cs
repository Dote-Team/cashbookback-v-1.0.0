using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Swashbuckle.AspNetCore.SwaggerGen;
using cashbook.Config;
using cashbook.Data;
using cashbook.Data.Interceptors;
using cashbook.Dto.Book;
using cashbook.Interfaces;
using cashbook.Middleware;
using cashbook.Models;
using cashbook.Repositories;
using cashbook.Services;

[CompilerGenerated]
internal class Program
{
	private static async Task Main(string[] args)
	{
		// ترميز المخرجات: بدونه تظهر الرسائل العربية (ومنها رسالة المفتاح الناقص
		// عند الإقلاع) مشوّهةً في طرفية الخادم أو في ملف السجل. لا يُفشل الإقلاع
		// إن تعذّر ضبطه، كأن تكون المخرجات معاد توجيهها.
		try
		{
			Console.OutputEncoding = System.Text.Encoding.UTF8;
		}
		catch (Exception)
		{
		}
		// توليد مفتاح توقيع جديد — يوضع قبل فحص المفتاح أدناه.
		// لو وُضع بعده لما أمكن توليد مفتاح على خادم لم يُضبط فيه بعد،
		// وهي الحالة التي يُحتاج فيها هذا الأمر بالضبط.
		if (Array.IndexOf(args, "--generate-secret") >= 0)
		{
			byte[] secretBytes = new byte[48];
			System.Security.Cryptography.RandomNumberGenerator.Fill(secretBytes);
			string generatedSecret = Convert.ToBase64String(secretBytes);
			Console.WriteLine("مفتاح توقيع جديد (ضعه في .env كما هو):");
			Console.WriteLine("ApiSettings__Secret=" + generatedSecret);
			return;
		}
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
		builder.Configuration.AddDotEnvFile();
		builder.Configuration.AddUserSecrets<Program>(optional: true);
		string key = builder.Configuration.GetValue<string>("ApiSettings:Secret");
		if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
		{
			throw new InvalidOperationException("مفتاح توقيع الرموز (ApiSettings:Secret) غير مضبوط أو أقصر من 32 حرفا\u064b.\nللتطوير : dotnet user-secrets set \"ApiSettings:Secret\" \"<قيمة عشوائية 32 حرفا\u064b على الأقل>\"\nعلى الخادم: أضف السطر  ApiSettings__Secret=<قيمة عشوائية 32 حرفا\u064b على الأقل>  إلى ملف .env\n            (ول\u0651د قيمة جديدة خاصة بالخادم، لا تستخدم مفتاح جهاز التطوير)");
		}
		string[] allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>();
		builder.Services.AddCors(delegate(CorsOptions options)
		{
			options.AddPolicy("AllowConfiguredOrigins", delegate(CorsPolicyBuilder policy)
			{
				policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()
					.AllowCredentials();
			});
		});
		builder.Services.AddControllers(delegate(MvcOptions options)
		{
			options.Filters.Add<ApiErrorSanitizerFilter>();
		}).AddNewtonsoftJson(delegate(MvcNewtonsoftJsonOptions options)
		{
			options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
			options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
		});
		builder.Services.AddEndpointsApiExplorer();
		builder.Services.AddSwaggerGen(delegate(SwaggerGenOptions c)
		{
			c.SwaggerDoc("v1", new OpenApiInfo
			{
				Title = "CashBook",
				Version = "v1"
			});
			c.EnableAnnotations();
			c.UseInlineDefinitionsForEnums();
			c.MapType<SortField>(() => new OpenApiSchema
			{
				Type = "string",
				Enum = ((IEnumerable<string>)Enum.GetNames(typeof(SortField))).Select((Func<string, IOpenApiAny>)((string n) => new OpenApiString(n))).ToList()
			});
			c.MapType<SortDirection>(() => new OpenApiSchema
			{
				Type = "string",
				Enum = ((IEnumerable<string>)Enum.GetNames(typeof(SortDirection))).Select((Func<string, IOpenApiAny>)((string n) => new OpenApiString(n))).ToList()
			});
			OpenApiSecurityScheme securityScheme = new OpenApiSecurityScheme
			{
				Name = "Authorization",
				Description = "JWT Authorization header using the Bearer scheme",
				Type = SecuritySchemeType.Http,
				Scheme = "bearer",
				BearerFormat = "JWT"
			};
			c.AddSecurityDefinition("Bearer", securityScheme);
			c.AddSecurityRequirement(new OpenApiSecurityRequirement { 
			{
				new OpenApiSecurityScheme
				{
					Reference = new OpenApiReference
					{
						Type = ReferenceType.SecurityScheme,
						Id = "Bearer"
					}
				},
				new List<string>()
			} });
		});
		builder.Services.AddAuthentication("Bearer").AddJwtBearer(delegate(JwtBearerOptions options)
		{
			options.RequireHttpsMetadata = false;
			options.SaveToken = true;
			options.TokenValidationParameters = new TokenValidationParameters
			{
				ValidateIssuerSigningKey = true,
				IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(key)),
				ValidateIssuer = false,
				ValidateAudience = false,
				ValidateLifetime = true,
				ClockSkew = TimeSpan.Zero
			};
		});
		builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
		builder.Services.AddDbContext<ApplicationDbContext>(delegate(IServiceProvider serviceProvider, DbContextOptionsBuilder option)
		{
			option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
			option.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
		});
		builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
		builder.Services.AddScoped<IBusinessRepository, BusinessRepository>();
		builder.Services.AddScoped<IContactRepository, ContactRepository>();
		builder.Services.AddScoped<ICustomFieldRepository, CustomFieldRepository>();
		builder.Services.AddScoped<IBookRepository, BookRepository>();
		builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
		builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
		builder.Services.AddScoped<IUserRepository, UserRepository>();
		builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("MailSettings"));
		builder.Services.AddScoped<IEmailService, EmailService>();
		builder.Services.AddScoped<IBusinessUserRepository, BusinessUserRepository>();
		builder.Services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
		builder.Services.AddScoped<ISettingRepository, SettingRepository>();
		builder.Services.AddScoped<ITransactionHistoryRepository, TransactionHistoryRepository>();
		builder.Services.AddScoped<IExchangeRateRepository, ExchangeRateRepository>();
		builder.Services.AddScoped<IExchangeRateAuthorization, ExchangeRateAuthorization>();
		builder.Services.AddScoped<IBackupService, BackupService>();
		builder.Services.Configure<AuditSettings>(builder.Configuration.GetSection("AuditSettings"));
		builder.Services.AddSingleton<AuditLogger>();
		builder.Services.AddSingleton((Func<IServiceProvider, IAuditLogger>)((IServiceProvider sp) => sp.GetRequiredService<AuditLogger>()));
		builder.Services.AddSingleton<AuditSaveChangesInterceptor>();
		builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
		builder.Services.AddHostedService<AuditWriterService>();
		builder.Services.AddHostedService<AuditRetentionService>();
		builder.Services.AddSingleton<ILoginThrottle, LoginThrottle>();
		builder.Services.Configure(delegate(FormOptions options)
		{
			options.MultipartBodyLengthLimit = 209715200L;
		});
		builder.Services.Configure(delegate(KestrelServerOptions options)
		{
			options.Limits.MaxRequestBodySize = 209715200L;
		});
		WebApplication app = builder.Build();
		app.UseCors("AllowConfiguredOrigins");
		string rootFolder = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");
		if (!Directory.Exists(rootFolder))
		{
			Directory.CreateDirectory(rootFolder);
		}
		app.MapGet("/API/Files/{imageName}", (Func<string, IResult>)delegate(string imageName)
		{
			string contentRootPath = app.Environment.ContentRootPath;
			string path = Path.Combine(contentRootPath, "wwwroot", "uploads", imageName);
			if (!File.Exists(path))
			{
				return Results.NotFound("Image not found.");
			}
			string text = Path.GetExtension(imageName).TrimStart('.');
			string contentType = "image/" + text;
			return Results.File(path, contentType);
		}).WithDisplayName("ShowImage");
		if (app.Environment.IsDevelopment())
		{
			app.UseSwagger();
			app.UseSwaggerUI();
		}
		app.UseAuthentication();
		app.UseMiddleware<AuditLogMiddleware>(Array.Empty<object>());
		app.UseMiddleware<SessionValidationMiddleware>(Array.Empty<object>());
		app.UseAuthorization();
		app.MapControllers();
		int inspectIndex = Array.IndexOf(args, "--inspect-backup");
		if (inspectIndex >= 0 && args.Length > inspectIndex + 2)
		{
			using (IServiceScope inspectScope = app.Services.CreateScope())
			{
				IBackupService backupService = inspectScope.ServiceProvider.GetRequiredService<IBackupService>();
				await backupService.DumpAsync(args[inspectIndex + 1], args[inspectIndex + 2]);
			}
			return;
		}
		using (IServiceScope scope = app.Services.CreateScope())
		{
			ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			string adminUsername = app.Configuration.GetValue<string>("AdminUser:Username");
			string adminEmail = app.Configuration.GetValue<string>("AdminUser:Email");
			bool hasUsername = !string.IsNullOrWhiteSpace(adminUsername);
			bool hasEmail = !string.IsNullOrWhiteSpace(adminEmail);
			if (hasUsername | hasEmail)
			{
				string usernameLower = adminUsername?.Trim().ToLowerInvariant();
				string emailLower = adminEmail?.Trim().ToLowerInvariant();
				User admin = null;
				if (hasUsername)
				{
					admin = await dbContext.Users.FirstOrDefaultAsync((User u) => u.Username.ToLower() == usernameLower);
				}
				if ((admin == null) & hasEmail)
				{
					admin = await dbContext.Users.FirstOrDefaultAsync((User u) => u.Email.ToLower() == emailLower);
				}
				if (admin != null && !admin.IsSuperAdmin)
				{
					admin.IsSuperAdmin = true;
					admin.UpdatedAt = DateTime.Now;
					await dbContext.SaveChangesAsync();
				}
			}
		}
		app.Run();
	}
}
