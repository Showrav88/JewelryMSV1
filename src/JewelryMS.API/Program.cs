using Npgsql;
using Dapper;
using System.Data;
using System.Reflection;
using NpgsqlTypes;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using JewelryMS.Domain.Enums;
using JewelryMS.Domain.Interfaces.Repositories;
using JewelryMS.Infrastructure.Repositories;
using Microsoft.OpenApi.Models; 
using Microsoft.AspNetCore.Diagnostics.HealthChecks; 
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization; 
using JewelryMS.API.Authorization;     
using JewelryMS.Domain.Interfaces.Services;
using JewelryMS.Application.Services;
using JewelryMS.API.Middleware;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);

// --- 1. DATABASE & ENUMS ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);

// Map Database Enums to C# Enums
dataSourceBuilder.MapEnum<UserRole>("user_role");
dataSourceBuilder.MapEnum<MetalPurity>("metal_purity");
dataSourceBuilder.MapEnum<JewelryCategory>("jewelry_category");
dataSourceBuilder.MapEnum<MaterialType>("material_type");
dataSourceBuilder.MapEnum<StockStatus>("stock_status");
dataSourceBuilder.MapEnum<Payment_type>("payment_type");

var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);

// --- HEALTH CHECKS ---
builder.Services.AddHealthChecks().AddNpgSql(connectionString!);

// --- 2. CORS CONFIGURATION ---
// Essential for allowing Frontend (React/Vue/Mobile) to hit the API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// --- 3. DAPPER SETUP ---
DefaultTypeMap.MatchNamesWithUnderscores = true;
SqlMapper.AddTypeHandler(typeof(MetalPurity), new UniversalEnumHandler<MetalPurity>());
SqlMapper.AddTypeHandler(typeof(JewelryCategory), new UniversalEnumHandler<JewelryCategory>());
SqlMapper.AddTypeHandler(typeof(MaterialType), new UniversalEnumHandler<MaterialType>());
SqlMapper.AddTypeHandler(typeof(UserRole), new UniversalEnumHandler<UserRole>());
SqlMapper.AddTypeHandler(typeof(StockStatus), new UniversalEnumHandler<StockStatus>());
SqlMapper.AddTypeHandler(typeof(Payment_type), new UniversalEnumHandler<Payment_type>());

builder.Services.AddControllers()
    .AddApplicationPart(Assembly.GetExecutingAssembly())
    .AddControllersAsServices();

// --- 4. SWAGGER / OPENAPI ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Jewelry Management API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your token: Bearer {your_token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });
});

builder.Services.AddHttpContextAccessor();

// --- 5. DEPENDENCY INJECTION (Repositories & Services) ---
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IPublicProductRepository, PublicProductRepository>();
builder.Services.AddScoped<IMetalRateRepository, MetalRateRepository>();
builder.Services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
builder.Services.AddScoped<ISaleRepository, SaleRepository>();

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMetalRateService, MetalRateService>();
builder.Services.AddScoped<IPublicProductService, PublicProductService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISaleService, SaleService>();


// --- 6. AUTHORIZATION LOGIC ---
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

// --- 7. JWT AUTHENTICATION ---
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new Exception("JWT Key missing!");
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role, 
            ClockSkew = TimeSpan.Zero 
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    status = 401,
                    error = "Unauthorized",
                    message = "Access denied. A valid token is required."
                });
            },
            OnForbidden = async context => // Added 403 Forbidden Custom Message
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    status = 403,
                    error = "Forbidden",
                    message = "You do not have the required permissions for this action."
                });
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// --- 8. MIDDLEWARE PIPELINE ---
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "JewelryMS API v1"));
}

app.UseCors("AllowAll"); // Enable CORS
app.UseStaticFiles();
app.UseRouting();

app.MapHealthChecks("/health");

// CRITICAL ORDER: Authenticate -> Set SQL Session -> Authorize
app.UseAuthentication(); 
app.UseMiddleware<PermissionMiddleware>(); 
app.UseAuthorization();

app.MapControllers();
app.Run();

// --- 9. ENUM HANDLER ---
public class UniversalEnumHandler<T> : SqlMapper.TypeHandler<T> where T : struct, Enum
{
    public override void SetValue(IDbDataParameter parameter, T value) => parameter.Value = value.ToString();
    public override T Parse(object value)
    {
        if (value == null || value is DBNull) return default;
        string dbValue = value.ToString()!;
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var attr = field.GetCustomAttribute<PgNameAttribute>();
            if (attr != null && attr.PgName == dbValue) return (T)field.GetValue(null)!;
        }
        return Enum.TryParse<T>(dbValue, true, out var result) ? result : default;
    }
}