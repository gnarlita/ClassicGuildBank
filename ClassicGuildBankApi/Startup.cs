﻿using System;
using System.Text;
using ClassicGuildBankApi.Tools;
using ClassicGuildBankData.Data;
using ClassicGuildBankData.Models;
using ClassicGuildBankData.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace SSIndustrialApi
{
    public class Startup
    {
        #region Data Members

        private readonly IConfiguration _configuration;

        private readonly IHostingEnvironment _hostingEnvironment;

        #endregion

        #region Constructor

        public Startup(IConfiguration configuration, IHostingEnvironment hostingEnvironment)
        {
            _configuration = configuration;
            _hostingEnvironment = hostingEnvironment;
        }
        
        #endregion

        #region Public Methods

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();

            services.AddIdentity<ClassicGuildBankUser, IdentityRole>()
                .AddEntityFrameworkStores<ClassicGuildBankDbContext>()
                .AddDefaultTokenProviders();

            services.Configure<IdentityOptions>(options =>
            {
               options.Password.RequireDigit = true;
               options.Password.RequiredLength = 8;
               options.Password.RequireNonAlphanumeric = false;
               options.Password.RequireUppercase = true;
               options.Password.RequireLowercase = true;
            });


            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

                })
                .AddJwtBearer(cfg =>
                {
                    cfg.RequireHttpsMetadata = false;
                    cfg.SaveToken = true;
                    cfg.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = _configuration["JwtIssuer"],
                        ValidAudience = _configuration["JwtIssuer"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtKey"])),
                        ClockSkew = TimeSpan.Zero // remove delay of token when expire
                    };
                });

            services.AddMvc(options => {
                options.EnableEndpointRouting = false;
                if ( _hostingEnvironment.IsProduction() )
                    options.Filters.Add(new RequireHttpsAttribute());
            }).AddJsonOptions(jsonOptions => {
                jsonOptions.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            }).AddNewtonsoftJson(options => {
                options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
            });

            services.AddDbContext<ClassicGuildBankDbContext>( options => options.UseSqlServer(_configuration.GetConnectionString("ClassicGuildBankDb")));

            services.AddTransient<ClassicGuildBankSeeder>();

            services.AddScoped<GuildBankRepository>();
            services.AddScoped<PatreonRepository>();
            services.AddScoped<SendGridService>();
            services.AddScoped<RazorService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseCors(
                    options => options.WithOrigins("http://localhost:4200", "https://notagb.com", "https://www.notagb.com")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials());

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            //app.UseMiddleware<JWTCookieMiddleware>();

            app.UseAuthentication();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        } 

        #endregion
    }
}
