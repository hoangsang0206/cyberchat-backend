using System;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using CyberChat.Application;
using CyberChat.Application.Common.Interfaces;
using CyberChat.Infrastructure;
using CyberChat.API.Hubs;
using CyberChat.API.Middlewares;
using CyberChat.API.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Register Clean Architecture Layer Services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 3. Register API Level Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ISignalRService, SignalRService>();

// 4. Configure Controllers with JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums as strings in HTTP responses
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 5. Configure SignalR with Redis Backplane option
var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
var signalrBuilder = builder.Services.AddSignalR();
if (!string.IsNullOrEmpty(redisConn) && redisConn != "none")
{
    signalrBuilder.AddStackExchangeRedis(redisConn);
}

// 6. Configure Swagger with Bearer authorization support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CyberChat API",
        Version = "v1",
        Description = "Real-time Chat and Social Platform API"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\""
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

// 7. Configure JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "SuperSecretKeyForCyberChatThatIsAtLeast32BytesLong!";
var key = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "CyberChat",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "CyberChat",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Extract access tokens from URL query strings for SignalR connections
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// 8. Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true) // Allow any origin in dev, customize for prod
            .AllowCredentials();
    });
});

var app = builder.Build();

// 9. Exception Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 10. Swagger Setup
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CyberChat API v1"));
}

// Enable Static Files (for local files storage uploads)
app.UseStaticFiles();

// Serve local uploads folder if it exists
var uploadsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseSerilogRequestLogging();
app.UseRouting();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// 11. Custom activity logger middleware
app.UseMiddleware<UserActivityMiddleware>();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
