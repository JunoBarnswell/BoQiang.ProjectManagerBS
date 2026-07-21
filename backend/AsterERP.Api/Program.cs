using AsterERP.Shared;
using AsterERP.Api.Infrastructure.Abp;
using AsterERP.Api.Infrastructure;
using AsterERP.Api.Infrastructure.Diagnostics;
using AsterERP.Api.Infrastructure.Errors;
using AsterERP.Api.Infrastructure.Logging;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Infrastructure.SignalR;
using AsterERP.Api.Infrastructure.Workflows;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.FileProviders;
using Serilog;
using System.Globalization;
using Volo.Abp;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables()
        .Build())
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/astererp-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();
    ConfigureStandaloneHostFiltering(builder);

    var supportedCultures = new[] { new CultureInfo("zh-CN"), new CultureInfo("en-US") };
    builder.Services.Configure<RequestLocalizationOptions>(options =>
    {
        options.DefaultRequestCulture = new RequestCulture("zh-CN");
        options.SupportedCultures = supportedCultures;
        options.SupportedUICultures = supportedCultures;
        options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
    });

    builder.Services.AddAsterErpInfrastructure(builder.Configuration, builder.Environment);

    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<AsterErpApiExceptionFilter>();
    });

    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
    builder.Services.AddSignalR();

    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var firstError = context.ModelState.Values
                .SelectMany(item => item.Errors)
                .Select(item => item.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                ?? "参数校验失败";

            return new BadRequestObjectResult(ApiResultFactory.Fail<object?>(
                firstError,
                context.HttpContext.TraceIdentifier,
                ErrorCodes.ParameterInvalid));
        };
    });

    builder.Services.AddCors(options =>
    {
        var frontendOrigins = new[]
        {
            builder.Configuration["Cors:FrontendOrigin"] ?? "http://localhost:5173",
            "http://127.0.0.1:5173",
            "http://[::1]:5173"
        };

        options.AddPolicy("FrontendDevelopment", policy =>
        {
            policy
                .WithOrigins(frontendOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    await builder.AddApplicationAsync<AsterErpAbpHostModule>();

    var app = builder.Build();

    await app.InitializeApplicationAsync();
    app.UseRequestLocalization(new RequestLocalizationOptions
    {
        DefaultRequestCulture = new RequestCulture("zh-CN"),
        SupportedCultures = supportedCultures,
        SupportedUICultures = supportedCultures,
        RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()]
    });
    app.UseExceptionHandler("/error");
    app.UseMiddleware<RequestDiagnosticsMiddleware>();
    app.UseMiddleware<CurrentUserMiddleware>();
    app.UseMiddleware<WorkflowWorkspaceRuntimeMiddleware>();
    app.UseMiddleware<DataPermissionFilterMiddleware>();
    app.UseMiddleware<OperationLogMiddleware>();
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (context, httpContext) =>
        {
            context.Set("TraceId", httpContext.TraceIdentifier);
            context.Set("UserId", httpContext.User.FindFirst(AsterErpClaimTypes.UserId)?.Value);
            context.Set("Path", httpContext.Request.Path);
        };
    });

    app.UseRouting();
    app.UseCors("FrontendDevelopment");
    app.UseRateLimiter();
    var processDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
    var staticRoot = new[]
    {
        app.Environment.WebRootPath,
        Path.Combine(app.Environment.ContentRootPath, "wwwroot"),
        Path.Combine(app.Environment.ContentRootPath, "backend", "AsterERP.Api", "wwwroot"),
        Path.Combine(processDirectory, "wwwroot")
    }
    .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
    ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(staticRoot)
    });

    app.Map("/error", (HttpContext context, GlobalExceptionHandler handler) => handler.Handle(context))
        .ExcludeFromDescription();

    app.MapControllers();
    app.MapHub<SystemNotificationHub>("/hubs/system-notification");
    app.MapFallback(async context =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            var rootIndexPath = Path.Combine(staticRoot, "index.html");
            if (File.Exists(rootIndexPath))
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.SendFileAsync(rootIndexPath);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var appCode = segments[0].ToUpperInvariant();
        if (appCode == "ASSETS")
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var fallbackIndexPath = Path.Combine(staticRoot, "index.html");
        if (appCode.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch != '-' && ch != '_'))
        {
            if (Path.HasExtension(path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (File.Exists(fallbackIndexPath))
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.SendFileAsync(fallbackIndexPath);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var indexPath = Path.Combine(staticRoot, appCode, "index.html");
        if (!File.Exists(indexPath))
        {
            if (File.Exists(fallbackIndexPath))
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.SendFileAsync(fallbackIndexPath);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(indexPath);
    });

    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

static void ConfigureStandaloneHostFiltering(WebApplicationBuilder builder)
{
    if (!string.Equals(builder.Configuration["Deployment:Profile"], "Standalone", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    builder.Services.Configure<HostFilteringOptions>(options =>
    {
        options.AllowedHosts.Clear();
        options.AllowedHosts.Add("*");
    });
}
