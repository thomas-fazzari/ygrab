// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace YGrab.Host.Auth;

internal static class AuthExtensions
{
    internal const string APPLICATION_NAME = "ygrab";
    internal const string POLICY_NAME = "signed-in";

    extension(IServiceCollection services)
    {
        // The data folder is read when the services build, so a setting that a test host adds late counts
        public IServiceCollection AddYGrabAuth()
        {
            services
                .AddOptions<AuthOptions>()
                .BindConfiguration(
                    AuthOptions.SECTION_NAME,
                    HostConfigurationExtensions.FailOnUnknownKeys
                )
                .Validate<IHostEnvironment>(
                    (options, environment) => !options.Disabled || environment.IsDevelopment(),
                    $"{AuthOptions.SECTION_NAME}:{nameof(AuthOptions.Disabled)} works in the Development environment only."
                )
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();
            services.AddSingleton(provider => new HostStorage(
                Path.GetFullPath(
                    provider.GetRequiredService<IConfiguration>().GetYGrabDataDirectory()
                )
            ));
            services.AddSingleton<TokenFile>();
            services.AddSingleton<TokenStore>();
            services.AddSingleton<WrongTokenLimiter>();
            services.AddHostedService(provider => provider.GetRequiredService<TokenStore>());

            services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddScheme<AuthenticationSchemeOptions, BearerHandler>(
                    BearerHandler.SCHEME_NAME,
                    configureOptions: null
                )
                .AddCookie(ConfigureCookie);
            services
                .AddAuthorizationBuilder()
                .AddPolicy(POLICY_NAME, policy =>
                    policy
                        .AddAuthenticationSchemes(
                            BearerHandler.SCHEME_NAME,
                            CookieAuthenticationDefaults.AuthenticationScheme
                        )
                        .RequireAuthenticatedUser()
                );

            services.AddDataProtection().SetApplicationName(APPLICATION_NAME);
            services
                .AddOptions<KeyManagementOptions>()
                .Configure<HostStorage, ILoggerFactory>(
                    (options, storage, loggers) =>
                        options.XmlRepository = new FileSystemXmlRepository(
                            new DirectoryInfo(storage.KeysDirectory),
                            loggers
                        )
                );
            return services;
        }
    }

    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseYGrabAuth()
        {
            app.UseAuthentication();
            app.UseAuthorization();
            return app;
        }
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = SessionCookie.NAME;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = SessionCookie.Lifetime;
        options.Events.OnValidatePrincipal = SessionCookie.ValidateAsync;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }
}
