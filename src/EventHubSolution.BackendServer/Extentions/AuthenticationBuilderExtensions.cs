using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace EventHubSolution.BackendServer.Extentions
{
    public static class AuthenticationBuilderExtensions
    {
        public static WebApplicationBuilder AddAppAuthetication(this WebApplicationBuilder builder)
        {
            var settingsSection = builder.Configuration.GetSection("ApiSettings:JwtOptions");

            var secret = settingsSection.GetValue<string>("Secret");
            var issuer = settingsSection.GetValue<string>("Issuer");
            var audience = settingsSection.GetValue<string>("Audience");

            var key = Encoding.ASCII.GetBytes(secret);


            builder.Services
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
                        ValidIssuer = issuer,
                        ValidAudience = audience,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                    x.SaveToken = true;
                })
                .AddGoogle(googleOptions =>
                {
                    googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"];
                    googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

                    googleOptions.AccessDeniedPath = "/AccessDeniedPathInfo";
                    googleOptions.Scope.Add("profile");
                    googleOptions.SignInScheme = Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme;
                    googleOptions.SaveTokens = true;
                })
                .AddFacebook(facebookOptions =>
                {
                    facebookOptions.AppId = builder.Configuration["Authentication:Facebook:ClientId"];
                    facebookOptions.AppSecret = builder.Configuration["Authentication:Facebook:ClientSecret"];

                    facebookOptions.AccessDeniedPath = "/AccessDeniedPathInfo";
                    facebookOptions.SaveTokens = true;
                });


            return builder;
        }
    }
}
