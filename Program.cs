using cashbook.Data;
using cashbook.Dto.Book;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Serialization;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var key = builder.Configuration.GetValue<string>("ApiSettings:Secret");
var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowConfiguredOrigins", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();

    });
});

builder.Services.AddControllers().AddNewtonsoftJson(options =>
{
    options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();

});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CashBook", Version = "v1" });
    c.EnableAnnotations();
    c.UseInlineDefinitionsForEnums();
    c.MapType<SortField>(() => new OpenApiSchema
    {
        Type = "string",
        Enum = Enum.GetNames(typeof(SortField))
                   .Select(n => (IOpenApiAny)new OpenApiString(n))
                   .ToList()
    });

    c.MapType<SortDirection>(() => new OpenApiSchema
    {
        Type = "string",
        Enum = Enum.GetNames(typeof(SortDirection))
                   .Select(n => (IOpenApiAny)new OpenApiString(n))
                   .ToList()
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT Authorization header using the Bearer scheme",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
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
        }
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
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
builder.Services.AddDbContext<ApplicationDbContext>(option =>
{
    option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Dependency Injection Configuration
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




var app = builder.Build();
app.UseCors("AllowConfiguredOrigins");

var rootFolder = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");
if (!Directory.Exists(rootFolder))
{
    Directory.CreateDirectory(rootFolder);
}


app.MapGet("/API/Files/{imageName}", (string imageName) =>
{
    var contentRoot = app.Environment.ContentRootPath;
    var imagePath = Path.Combine(contentRoot, "wwwroot", "uploads", imageName);
    if (!System.IO.File.Exists(imagePath))
    {
        return Results.NotFound("Image not found.");
    }

    var imageExtension = Path.GetExtension(imageName).TrimStart('.');
    var contentType = $"image/{imageExtension}";

    return Results.File(imagePath, contentType);
}).WithDisplayName("ShowImage");



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseMiddleware<SessionValidationMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
