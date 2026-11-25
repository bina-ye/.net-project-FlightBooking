using FlightBooking.API.Middleware;
using FlightBooking.Core.Interfaces;
using FlightBooking.Data.Context;
using FlightBooking.Data.Repositories;
using FlightBooking.Data.Seed;
using FlightBooking.Service.Mapping;
using FlightBooking.Service.Payment;
using FlightBooking.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NLog.Web;
using System.Text;
//using AutoMapper;

var builder = WebApplication.CreateBuilder(args);
// ---------- NLog ----------
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// ---------- Data ----------
builder.Services.AddDbContext<FlightBookingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IFlightRepository, FlightRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// ---------- Service ----------
builder.Services.AddScoped<IFlightService, FlightService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPaymentService, MockPaymentService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<MappingProfile>();
});
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

// משתמשי הדמו — המיילים והסיסמאות ניתנים להגדרה דרך קונפיגורציה.
// אם לא הוגדרה סיסמה, נופלים חזרה לברירת המחדל של הנתונים לדוגמה.
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection("Seed"));

// ---------- JWT ----------
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing from configuration.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
// ---------- CORS (לצד הלקוח בפיתוח) ----------

    const string ClientCorsPolicy = "ClientPolicy";

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(ClientCorsPolicy, policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5500",                 // Live Server
                    "http://127.0.0.1:5500",
                    "https://flyby-18gb.onrender.com")            // ← הלקוח ב-Render
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
builder.Services.AddEndpointsApiExplorer();


builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "הזיני: {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ---------- Migration + Seed ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FlightBookingDbContext>();
    var seedOptions = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
    await DbSeeder.SeedAsync(db, seedOptions, ct: default);
}

// Render מזריק PORT (ברירת מחדל 10000) — מאזינים לו על כל הממשקים
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    app.Urls.Clear();
    app.Urls.Add($"http://0.0.0.0:{port}");
}

// ---------- Pipeline ----------
app.UseCorrelationId();
app.UseErrorHandling();
app.UseShabbat();
app.UseCors(ClientCorsPolicy);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // הגשת הלקוח הסטטי מהשרת עצמו בפיתוח: http://localhost:5069/index.html
    // כך אין צורך ב-Live Server (שמרענן את הדף בכל שינוי קובץ) ואין בעיית CORS.
    // bin/Debug/net9.0 → 5 רמות למעלה = תיקיית "project .net", ואז לתוך הלקוח.
    // fix: שימוש מפורש בשם התיקייה כדי שלא תיווצר טעות בשם קבצים/תיקיות.
    var clientPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "FlightBooking.Client"));

    if (Directory.Exists(clientPath))
    {
        var clientFileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(clientPath);

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = clientFileProvider });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = clientFileProvider,
            OnPrepareResponse = ctx =>
                ctx.Context.Response.Headers.CacheControl = "no-cache"
        });
    }
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();