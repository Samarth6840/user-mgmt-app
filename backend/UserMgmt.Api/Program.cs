using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using UserMgmt.Api.Data;
using UserMgmt.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connString = builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "No database connection string found. Set DATABASE_URL or ConnectionStrings:Default.");

// Render provides a URI-style URL (postgresql://user:pass@host/db), but Npgsql
// only parses keyword format (Host=...;Username=...;...). Convert if needed.
if (Uri.TryCreate(connString, UriKind.Absolute, out var dbUri)
    && dbUri.Scheme is "postgresql" or "postgres")
{
    var userInfo = dbUri.UserInfo.Split(':', 2);
    connString = new NpgsqlConnectionStringBuilder
    {
        Host = dbUri.Host,
        Port = dbUri.Port > 0 ? dbUri.Port : 5432,
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        Database = dbUri.AbsolutePath.TrimStart('/')
    }.ConnectionString;
}

Console.WriteLine($"DB connection string loaded ({connString.Length} chars, host={dbUri?.Host ?? "n/a"}).");

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(connString));

builder.Services.AddSingleton<EmailDispatcher>();
builder.Services.AddHostedService<EmailDispatcherHostedService>();
builder.Services.AddScoped<ActiveUserFilter>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddHttpClient<EmailService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure JWT authentication so only valid, non-expired tokens are accepted.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
            NameClaimType = "sub"
        };

        // Return a JSON body on auth failures so the frontend can redirect to login.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                var result = System.Text.Json.JsonSerializer.Serialize(new { message = "Not authenticated.", redirectToLogin = true });
                return context.Response.WriteAsync(result);
            },
            OnForbidden = context =>
            {
                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/json";
                var result = System.Text.Json.JsonSerializer.Serialize(new { message = "Forbidden." });
                return context.Response.WriteAsync(result);
            }
        };
    });

builder.Services.AddAuthorization();

// Allow the frontend dev server to call the API during development.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration["App:CorsOrigins"]?.Split(',') ?? new[] { "http://localhost:5173" };
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// Create the database schema if it doesn't already exist.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // EnsureCreated never alters existing tables, so databases created by an
    // older build are patched here. Both statements are idempotent.
    try
    {
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"EmailVerified\" boolean NOT NULL DEFAULT false");
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"Users\" SET \"EmailVerified\" = TRUE WHERE \"Status\" = 'Active' AND \"EmailVerified\" = FALSE");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Schema patch skipped ({ex.Message}); table will be created fresh.");
    }

    await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
