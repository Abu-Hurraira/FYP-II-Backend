using System.Text;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Options;
using FolderManagementSystemAPI.Security;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwtOptions.Key) || jwtOptions.Key.Length < 32)
{
    throw new InvalidOperationException(
        "Configure Jwt:Key in appsettings (at least 32 characters) before starting the API.");
}

builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<EnforceCallerUserIdFilter>();

builder.Services.AddControllers(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
    options.Filters.Add<EnforceCallerUserIdFilter>();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<FolderManagementDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim type URIs as written in the token (user id / role).
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromMinutes(2),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<GraderAccessService>();
builder.Services.AddScoped<TeacherPermissionService>();
builder.Services.AddScoped<HodCoursesDashboardService>();
builder.Services.AddScoped<SessionScopeService>();
builder.Services.AddScoped<ContentSubmissionService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<CloAttainmentService>();
builder.Services.AddScoped<FolderDraftStorageService>();
builder.Services.AddScoped<FolderAutoContentService>();
builder.Services.AddScoped<CourseFolderProvisioningService>();
builder.Services.AddScoped<AssessmentCourseFolderLinkService>();
builder.Services.AddScoped<AssessmentFolderSyncService>();
builder.Services.AddScoped<CourseResultService>();
builder.Services.AddScoped<FcarReportService>();
builder.Services.AddScoped<CourseCatalogVersionService>();
builder.Services.AddScoped<OutcomeCatalogVersionService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactNative", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
                  .AllowAnyMethod()
                  .WithExposedHeaders("Content-Disposition");
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

//app.UseHttpsRedirection();

app.UseCors("AllowReactNative");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
