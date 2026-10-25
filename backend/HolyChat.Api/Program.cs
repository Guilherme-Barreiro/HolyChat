using System.Text;
using HolyChat.Api.Data;
using HolyChat.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using HolyChat.Api;
using HolyChat.Api.Models;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

builder.Services.AddControllers();

builder.Services.AddDbContext<HolyChatDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Serviços de auth
builder.Services.AddHttpClient<IMatrixAuthService, MatrixAuthService>();
builder.Services.AddHttpClient<ISynapseAdminService, SynapseAdminService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<ITotpService, TotpService>();

// JWT Bearer authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
        // JWT claims describe the state at issuance. Check the database on
        // every request so suspension, removal and demotion take effect at once.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?.FindFirst("sub")?.Value;
                if (!Guid.TryParse(userIdValue, out var userId))
                {
                    context.Fail("Invalid user identifier");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<HolyChatDbContext>();
                var currentUser = await db.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);
                var role = context.Principal?.FindFirst(ClaimTypes.Role)?.Value;
                if (currentUser is null || currentUser.Status != UserStatus.Active ||
                    role != currentUser.Role.ToString())
                {
                    context.Fail("Account or permissions changed");
                }
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFlutterDev", policy =>
    {
        policy.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AllowFlutterDev");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "HolyChat API v1");
    });
}
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();