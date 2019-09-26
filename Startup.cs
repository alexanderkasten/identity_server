// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using IdentityServer4;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.IO;
using IdentityServer4.AspNetIdentity;
using IdentityExpress.Identity;
using ProcessEngine.IdentityServer.Web.Features;

namespace IdentityServer
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; }

        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var builder = new ConfigurationBuilder()
                .SetBasePath(currentDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
            Environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<IClaimCheck, ClaimCheck>();

            services.AddDbContext<IdentityExpressDbContext>(options =>
            {
                options.UseSqlite(Configuration.GetConnectionString("Users"));
            });

            services.AddIdentity<IdentityExpressUser, IdentityExpressRole>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 5;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
            })
                .AddUserStore<IdentityExpressUserStore>()
                .AddRoleStore<IdentityExpressRoleStore>()
                .AddIdentityExpressUserClaimsPrincipalFactory()
                .AddDefaultTokenProviders();

            services.AddMvc();

            services.Configure<IISOptions>(iis =>
            {
                iis.AuthenticationDisplayName = "Windows";
                iis.AutomaticAuthentication = false;
            });

            var connectionString = Configuration.GetConnectionString("Configuration");
            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            var builder = services.AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;
            })
                .AddAspNetIdentity<IdentityExpressUser>()
                // this adds the config data from DB (clients, resources, CORS)
                .AddConfigurationStore(options =>
                {
                    options.ConfigureDbContext = db =>
                        db.UseSqlite(connectionString,
                            sql => sql.MigrationsAssembly(migrationsAssembly));
                })
                // // this adds the operational data from DB (codes, tokens, consents)
                .AddOperationalStore(options =>
                {
                    options.ConfigureDbContext = db =>
                        db.UseSqlite(connectionString,
                            sql => sql.MigrationsAssembly(migrationsAssembly));

                    // this enables automatic token cleanup. this is optional.
                    options.EnableTokenCleanup = true;
                    // options.TokenCleanupInterval = 15; // interval in seconds. 15 seconds useful for debugging
                });

            if (Environment.IsDevelopment())
            {
                builder.AddDeveloperSigningCredential();
            }
            else
            {
                builder.AddDeveloperSigningCredential();
            }

            services.AddAuthentication()
                .AddGoogle(options =>
                {
                    options.ClientId = "708996912208-9m4dkjb5hscn7cjrn5u0r4tbgkbj1fko.apps.googleusercontent.com";
                    options.ClientSecret = "wdfPY6t8H8cecgjlxud__4Gh";
                });

            var MicrosoftClientId = Configuration["MICROSOFT_CLIENT_ID"];
            var MicrosoftClientSecret = Configuration["MICROSOFT_CLIENT_SECRET"];

            if (!String.IsNullOrEmpty(MicrosoftClientId) && !String.IsNullOrEmpty(MicrosoftClientSecret))
            {
                services.AddAuthentication()
                    .AddMicrosoftAccount(options =>
                    {
                        options.ClientId = MicrosoftClientId;
                        options.ClientSecret = MicrosoftClientSecret;
                    });
            }

            services.UseAdminUI();
        }

        public void Configure(IApplicationBuilder app)
        {
            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseCors(builder =>
                builder.WithOrigins("http://localhost:9000", "http://localhost:17290", "https://bpmn.kasten.pw")
                       .AllowAnyHeader()
                       .AllowAnyMethod()
                       .AllowCredentials()
                );
                
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseIdentityServer();
            app.UseAuthentication();
            app.UseMvcWithDefaultRoute();
            app.UseAdminUI();
        }
    }
}
