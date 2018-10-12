using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using YoJanus.Web.Models;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Twitter;
using Microsoft.AspNetCore.Http;
using YoJanus.Web.Config;
using Microsoft.AspNetCore.DataProtection;
using Amazon.Extensions.NETCore.Setup;
using Amazon.S3;
using Amazon.S3.Model;
using System.Threading;
using System.IO;

namespace YoJanus.Web
{
    public class Startup
    {
        public Startup(IHostingEnvironment env, IConfiguration configuration)
        {
            this.Configuration = configuration;
            this._hostingEnvironment = env;
        }

        public IConfiguration Configuration { get; }

        private IHostingEnvironment _hostingEnvironment { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc(options =>
            {
                if (!this._hostingEnvironment.IsDevelopment()) options.Filters.Add(new RequireHttpsAttribute());
            });

            services.AddDefaultAWSOptions(Configuration.GetAWSOptions());
            services.AddAWSService<IAmazonS3>();

            //this method retrieves the envnvironment-specific settings file from a secure s3 bucket
            //and writes it to the file system on the server
            LoadAppSettingsFromS3();

            services.AddMvcCore().AddJsonFormatters(j => j.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore);
            
            services.AddTransient<IUserRepository, FakeUserRepository>();

            //this.Configuration["ConnectionStrings"] = dbCreds;
            services.AddDbContext<SurveyTestContext>(options => options.UseSqlServer(this.Configuration.GetConnectionString("yojanus")));

            services.AddDataProtection().SetApplicationName("YoJanus");
            services.AddS3KeyStorage(this.Configuration.GetSection("keyStorage:s3"));

            // https://docs.microsoft.com/en-us/aspnet/core/migration/1x-to-2x/identity-2x
            services.AddAuthentication("Cookies")
                .AddCookie("Cookies", cookieOptions =>
                {
                    cookieOptions.LoginPath = "/Account/Login";
                    cookieOptions.AccessDeniedPath = "/Account/AccessDenied";
                })
                .AddGoogle("Google", googleOptions =>
                {
                    googleOptions.SignInScheme = "Cookies";
                    googleOptions.ClientId = this.Configuration["Authentication:Google:ClientID"] ?? "431326683697-fbsbqm647eeqklttm80kge0nuo39j7rl.apps.googleusercontent.com";
                    googleOptions.ClientSecret = this.Configuration["Authentication:Google:ClientSecret"] ?? "um16gPR_ZASRc5hUS2KduVqc";
                    googleOptions.CallbackPath = "/Login/Google";
                    googleOptions.Events.OnCreatingTicket = ctx => GetOrCreateExternalUser(ctx.Identity, AuthType.google, ctx.HttpContext);
                })
                .AddFacebook("Facebook", facebookOptions =>
                {
                      facebookOptions.AppId = this.Configuration["Authentication:Facebook:AppId"] ?? "184463922091684";
                      facebookOptions.AppSecret = this.Configuration["Authentication:Facebook:AppSecret"] ?? "265cf8938e4857cbdd04f054a4dabb4b";
                      facebookOptions.CallbackPath = "/Login/Facebook";
                      facebookOptions.SignInScheme = "Cookies";
                      facebookOptions.Events.OnCreatingTicket = ctx => GetOrCreateExternalUser(ctx.Identity, AuthType.facebook, ctx.HttpContext);
                })
                .AddTwitter("Twitter", twitterOptions =>
                {
                    twitterOptions.ConsumerKey = this.Configuration["Authentication:Twitter:ConsumerKey"] ?? "062l1BBM0FYZF9swGocCRpLq7";
                    twitterOptions.ConsumerSecret = this.Configuration["Authentication:Twitter:ConsumerSecret"] ?? "JILDZjNFBqDprv5g4GNXR9Fz7sYhTu7VcSYc9D3t7hrGa9SZcn";
                    twitterOptions.CallbackPath = "/Login/Twitter";
                    twitterOptions.SignInScheme = "Cookies";
                    twitterOptions.RetrieveUserDetails = true;
                    twitterOptions.Events.OnCreatingTicket = ctx => {
                        var identity = ctx.Principal.Identities.First();
                        // twitter doesn't have first/last names, just a single name field
                        // on top of that, full name isn't added to the Identity automatically but it is in the User JObject
                        var fullName = ctx.User.Value<string>("name");
                        string firstName, lastName;
                        // attempt to split on space and take all but the last part as firstname
                        // naive but ¯\_(ツ)_/¯
                        var split = fullName.Split(new [] {' '}, StringSplitOptions.RemoveEmptyEntries);
                        if (split.Length == 1)
                        {
                            firstName = fullName;
                            lastName = null;
                        }
                        else
                        {
                            firstName = string.Join(" ", split.Take(split.Length-1).Select(n=>n.Trim()));
                            lastName = split.Last().Trim();
                        }
                        // just to make the claims look similar to the other providers, add first/last name claims
                        identity.AddClaim(new Claim(ClaimTypes.GivenName, firstName));
                        if (!string.IsNullOrEmpty(lastName))
                            identity.AddClaim(new Claim(ClaimTypes.Surname, lastName));
                        
                        return GetOrCreateExternalUser(identity, AuthType.twitter, ctx.HttpContext);
                    };
                });

                // private helper method since it's the same almost for all types
            async Task GetOrCreateExternalUser(ClaimsIdentity identity, AuthType authenticationType, HttpContext httpContext) {
                var appId = identity.FindFirst(ClaimTypes.NameIdentifier).Value;
                var firstName = identity.FindFirst(ClaimTypes.GivenName)?.Value; // first or last name may be null depending on what we get back from the auth providers
                var lastName = identity.FindFirst(ClaimTypes.Surname)?.Value;
                var email = identity.FindFirst(ClaimTypes.Email)?.Value;
                var userRepo = httpContext.RequestServices.GetService<IUserRepository>();
                var userId = await userRepo.GetOrCreateExternalUser(authenticationType, appId, firstName, lastName, email);
                identity.AddClaim(new Claim("YoJanus.ID", userId.ToString()));
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(this.Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            var forwardingHeaders = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.All, 
                RequireHeaderSymmetry = false
            };
            forwardingHeaders.KnownNetworks.Clear();
            forwardingHeaders.KnownProxies.Clear();
            app.UseForwardedHeaders(forwardingHeaders);

            if (!env.IsDevelopment())
            {
                var rewriteOptions = new RewriteOptions().AddRedirectToHttps();
                app.UseRewriter(rewriteOptions);
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();
            app.UseAuthentication();

            //app.UseStatusCodePagesWithRedirects("/Error/{0}");
            app.UseStatusCodePagesWithReExecute("/Error/{0}");

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{friendlyText?}/{id?}");
            });
        }
        private async Task LoadAppSettingsFromS3(){
            AmazonS3Client s3 = new AmazonS3Client(Amazon.RegionEndpoint.USEast1);
            string responseBody;
            var bucketName = "yojanus."+_hostingEnvironment.EnvironmentName.ToLower().ToString();
            var filename = "appsettings."+_hostingEnvironment.EnvironmentName.ToString()+".json";
            var request = new GetObjectRequest{
                BucketName = bucketName,
                Key = filename
            };
            var fileProvider = this._hostingEnvironment.ContentRootFileProvider;
            var fileInfo = fileProvider.GetFileInfo(filename);
            var physicalPath = fileInfo.PhysicalPath;
                using (var response = await s3.GetObjectAsync(request))
                using (var responseStream = response.ResponseStream)
                using (var reader = new StreamReader(responseStream))
                {
                        responseBody = reader.ReadToEnd();
                }

                File.WriteAllText(physicalPath,responseBody);
        }
    }
}