using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Services;
using EventHubSolution.BackendServer.Services.Interfaces;
using EventHubSolution.ViewModels;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.General;
using EventHubSolution.ViewModels.Systems;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;

namespace EventHubSolution.BackendServer.Extentions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, string appCors)
        {
            services.AddControllers();

            services.AddAppConfigures(configuration);

            services.ConfigureSwagger();

            services.ConfigureApplicationDbContext(configuration);

            services.AddSignalR();

            services.AddCors(p =>
                p.AddPolicy(appCors, build => { build.WithOrigins("*").AllowAnyMethod().AllowAnyHeader(); }));

            // 2.Setup identity
            services.AddIdentity<User, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddTokenProvider<DataProtectorTokenProvider<User>>(TokenProviders.GOOGLE)
                .AddTokenProvider<DataProtectorTokenProvider<User>>(TokenProviders.FACEBOOK)
                .AddDefaultTokenProviders();

            services.AddControllers();
            services.AddControllers()
                .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
                .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<RoleCreateRequestValidator>());

            services.AddInfrastructureServices();

            services.AddAutoMapper(cfg => cfg.AddProfile(new MappingProfile()));

            return services;
        }

        private static IServiceCollection ConfigureSwagger(this IServiceCollection services)
        {
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(option =>
            {
                option.AddSecurityDefinition(name: JwtBearerDefaults.AuthenticationScheme, securityScheme: new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Description = "Enter the Bearer Authorization PageOrder as following: `Bearer Generated-JWT-Token`",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                option.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = JwtBearerDefaults.AuthenticationScheme
                            }
                        },
                        new string[] { }
                    }
                });
                option.AddSignalRSwaggerGen();
            });

            return services;
        }

        private static IServiceCollection AddAppConfigures(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

            services.Configure<JwtOptions>(configuration.GetSection("ApiSettings:JwtOptions"));

            services.Configure<AzureBlobStorage>(configuration.GetSection("AzureBlobStorage"));

            services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));

            services.Configure<IdentityOptions>(options =>
            {
                // Default Lockout settings.
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;
                options.SignIn.RequireConfirmedPhoneNumber = false;
                options.SignIn.RequireConfirmedAccount = false;
                options.SignIn.RequireConfirmedEmail = false;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.User.RequireUniqueEmail = true;
            });

            services.Configure<DataProtectionTokenProviderOptions>(options =>
            {
                options.TokenLifespan = TimeSpan.FromHours(8);
            });

            services.Configure<CookiePolicyOptions>(options =>
            {
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
            });

            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.SuppressModelStateInvalidFilter = true;
            });

            return services;
        }

        private static IServiceCollection ConfigureApplicationDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            services.AddDbContext<ApplicationDbContext>(m => m.UseSqlServer(connectionString));

            return services;
        }

        private static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            services.AddTransient<DbInitializer>()
                .AddTransient<IEmailService, EmailService>()
                .AddTransient<ISequenceService, SequenceService>()
                .AddTransient<IFileStorageService, FileStorageService>()
                .AddTransient<ITokenService, TokenService>()
                .AddTransient<ICacheService, CacheService>();

            services.AddSingleton<AzureBlobService>();

            return services;
        }
    }
}
