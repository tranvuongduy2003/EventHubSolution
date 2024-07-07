using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Services;
using EventHubSolution.BackendServer.Services.Interfaces;
using EventHubSolution.ViewModels.Configurations;
using EventHubSolution.ViewModels.Constants;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventHubSolution.BackendServer.Extentions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, string appCors)
        {
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

                });
            services.AddCors(p =>
                p.AddPolicy(appCors, build =>
                {
                    build
                    .WithOrigins("*")
                    .AllowAnyMethod()
                    .AllowAnyHeader();
                }));
            services.AddIdentity<User, Role>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddTokenProvider<DataProtectorTokenProvider<User>>(TokenProviders.GOOGLE)
                .AddTokenProvider<DataProtectorTokenProvider<User>>(TokenProviders.FACEBOOK)
                .AddDefaultTokenProviders();
            services
                .AddFluentValidationAutoValidation()
                .AddFluentValidationClientsideAdapters();
            services
                .AddAutoMapper(config => config.AddProfile(new MappingProfile()));

            services.ConfigureSwagger();
            services.ConfigureAppSettings(configuration);
            services.ConfigureApplication();
            services.ConfigureApplicationDbContext(configuration);
            services.ConfigureAzureSignalR(configuration);
            services.ConfigureAuthetication();
            services.ConfigureInfrastructureServices();

            return services;
        }

        public static IServiceCollection ConfigureAppSettings(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtOptions = configuration.GetSection(nameof(JwtOptions))
                .Get<JwtOptions>();
            services.AddSingleton<JwtOptions>(jwtOptions);

            var azureBlobStorage = configuration.GetSection(nameof(AzureBlobStorage))
                .Get<AzureBlobStorage>();
            services.AddSingleton<AzureBlobStorage>(azureBlobStorage);

            var emailSettings = configuration.GetSection(nameof(EmailSettings))
                .Get<EmailSettings>();
            services.AddSingleton<EmailSettings>(emailSettings);

            var authentication = configuration.GetSection(nameof(Authentication))
                .Get<Authentication>();
            services.AddSingleton<Authentication>(authentication);

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
                        Array.Empty<string>()
                    }
                });
                option.AddSignalRSwaggerGen();
            });

            return services;
        }

        private static IServiceCollection ConfigureApplication(this IServiceCollection services)
        {
            services.Configure<RouteOptions>(options => options.LowercaseUrls = true);
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

        private static IServiceCollection ConfigureAuthetication(this IServiceCollection services)
        {
            var jwtOptions = services.GetOptions<JwtOptions>("JwtOptions");
            var key = Encoding.ASCII.GetBytes(jwtOptions.Secret);

            var authentication = services.GetOptions<Authentication>("Authentication");
            var googleAuthentication = authentication.Google;
            var facebookAuthentication = authentication.Facebook;


            services
                .AddAuthentication(x =>
                {
                    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddCookie()
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, x =>
                {
                    x.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                    x.SaveToken = true;
                })
                .AddGoogle(googleOptions =>
                {
                    googleOptions.ClientId = googleAuthentication.ClientId;
                    googleOptions.ClientSecret = googleAuthentication.ClientSecret;

                    googleOptions.AccessDeniedPath = "/AccessDeniedPathInfo";
                    googleOptions.Scope.Add("profile");
                    googleOptions.SignInScheme = Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme;
                    googleOptions.SaveTokens = true;
                })
                .AddFacebook(facebookOptions =>
                {
                    facebookOptions.AppId = facebookAuthentication.ClientId;
                    facebookOptions.AppSecret = facebookAuthentication.ClientSecret;

                    facebookOptions.AccessDeniedPath = "/AccessDeniedPathInfo";
                    facebookOptions.SaveTokens = true;
                });

            return services;
        }

        private static IServiceCollection ConfigureApplicationDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (connectionString == null || string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException("DefaultConnection is not configured.");
            services.AddDbContext<ApplicationDbContext>(m => m.UseSqlServer(connectionString));
            return services;
        }

        private static IServiceCollection ConfigureAzureSignalR(this IServiceCollection services, IConfiguration configuration)
        {
            var azureSignalR = configuration.GetConnectionString("AzureSignalR");
            if (azureSignalR == null || string.IsNullOrEmpty(azureSignalR))
                throw new ArgumentNullException("AzureSignalR is not configured.");
            services
                .AddSignalR()
                .AddAzureSignalR(azureSignalR);
            return services;
        }

        private static IServiceCollection ConfigureInfrastructureServices(this IServiceCollection services) =>
            services.AddTransient<DbInitializer>()
                .AddTransient<IEmailService, EmailService>()
                .AddTransient<ISequenceService, SequenceService>()
                .AddTransient<IFileStorageService, FileStorageService>()
                .AddTransient<ITokenService, TokenService>()
                .AddTransient<ICacheService, CacheService>()
                .AddSingleton<AzureBlobService>();
    }
}
