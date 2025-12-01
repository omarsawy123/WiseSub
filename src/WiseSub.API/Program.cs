using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using WiseSub.API.Middleware;
using WiseSub.Application;
using WiseSub.Infrastructure;
using WiseSub.Infrastructure.BackgroundServices.Jobs;
using WiseSub.Infrastructure.Data;

// Configure Serilog early for startup logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("MachineName", Environment.MachineName)
    .WriteTo.Console(outputTemplate: 
        "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId:l} {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting WiseSub API");
    
    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog from configuration
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Hangfire", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("MachineName", Environment.MachineName)
        .Enrich.WithProperty("Application", "WiseSub.API")
        .WriteTo.Console(outputTemplate: 
            "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {SourceContext}{NewLine}  {Message:lj}{NewLine}{Exception}"));

    // Add services to the container
    builder.Services.AddControllers();

    // Configure Swagger/OpenAPI documentation
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Version = "v1",
            Title = "WiseSub API",
            Description = "Subscription Management System API - Automatically discover, track, and manage recurring subscriptions by analyzing email communications.",
            Contact = new OpenApiContact
            {
                Name = "WiseSub Support",
                Email = "support@wisesub.com"
            },
            License = new OpenApiLicense
            {
                Name = "MIT License"
            }
        });

        // Add JWT Bearer authentication to Swagger
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT"
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

        // Include XML comments for API documentation
        var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }

        // Enable annotations for better documentation
        options.EnableAnnotations();
    });

    // Add global exception handler
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // Add Application layer services
    builder.Services.AddApplication();

    // Add Infrastructure layer services (includes circuit breaker)
    builder.Services.AddInfrastructure(builder.Configuration);

    // Configure Hangfire for background job processing
    builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = Environment.ProcessorCount * 2;
        options.Queues = new[] { "default", "email-processing", "email-scanning", "alerts", "maintenance" };
    });

    // Configure Rate Limiting to protect against brute-force attacks
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        
        // Global policy - 100 requests per minute per IP
        options.AddPolicy("global", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 5
                }));
        
        // Strict policy for auth endpoints - 10 requests per minute per IP
        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 2
                }));
    });

    // Configure JWT Authentication
    var jwtSecret = builder.Configuration["Authentication:JwtSecret"] 
        ?? throw new InvalidOperationException("JWT secret not configured");
    var jwtIssuer = builder.Configuration["Authentication:JwtIssuer"] ?? "WiseSub";
    var jwtAudience = builder.Configuration["Authentication:JwtAudience"] ?? "WiseSub";

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

    builder.Services.AddAuthorization();

    // Configure CORS for frontend
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline
    
    // Enable Swagger UI in development and production (for API documentation)
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "WiseSub API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "WiseSub API Documentation";
        options.DefaultModelsExpandDepth(2);
        options.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Model);
        options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        options.EnableDeepLinking();
        options.DisplayRequestDuration();
    });
    
    // Add correlation ID middleware first to ensure all requests have a correlation ID
    app.UseCorrelationId();
    
    // Add Serilog request logging
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            if (httpContext.Items.TryGetValue("CorrelationId", out var correlationId))
            {
                diagnosticContext.Set("CorrelationId", correlationId);
            }
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });
    
    app.UseExceptionHandler();
    app.UseHttpsRedirection();
    app.UseCors("AllowFrontend");
    app.UseRateLimiter();

    // Enable Hangfire Dashboard (protected in production)
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        // In production, add authorization filter
        Authorization = builder.Environment.IsDevelopment() 
            ? Array.Empty<IDashboardAuthorizationFilter>() 
            : new[] { new HangfireAuthorizationFilter() }
    });

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // Ensure database is created
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<WiseSubDbContext>();
        dbContext.Database.EnsureCreated();
    }

    // Schedule recurring Hangfire jobs
    RecurringJob.AddOrUpdate<EmailScanningJob>(
        "scan-all-emails",
        job => job.ScanAllAccountsAsync(CancellationToken.None),
        "*/15 * * * *"); // Every 15 minutes

    RecurringJob.AddOrUpdate<AlertGenerationJob>(
        "generate-alerts",
        job => job.GenerateAlertsAsync(CancellationToken.None),
        "0 8 * * *"); // Daily at 8 AM UTC

    RecurringJob.AddOrUpdate<SubscriptionUpdateJob>(
        "update-subscriptions",
        job => job.UpdateAllSubscriptionsAsync(CancellationToken.None),
        "0 2 * * *"); // Daily at 2 AM UTC

    Log.Information("WiseSub API started successfully");
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
