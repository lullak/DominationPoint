using DominationPoint.Core.Domain;
using DominationPoint.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class CustomWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
{
    // Static database name shared across all instances
    private static readonly string DatabaseName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Configure JWT settings
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.Sources.Clear();

            var testConfig = new Dictionary<string, string>
            {
                ["Jwt:Key"] = "ThisIsASecretKeyForTestingPurposesOnly123456",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience"
            };

            config.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureTestServices(services =>
        {
            // CRITICAL: Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database with PERSISTENT name
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                // Use the SAME database name across all requests
                options.UseInMemoryDatabase(DatabaseName);
            });

            // Build and seed ONCE
            var sp = services.BuildServiceProvider();
            using (var scope = sp.CreateScope())
            {
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<ApplicationDbContext>();
                var userManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = scopedServices.GetRequiredService<RoleManager<IdentityRole>>();

                db.Database.EnsureCreated();

                // Seed the database
                SeedAsync(userManager, roleManager).GetAwaiter().GetResult();
            }
        });
    }

    public static async Task SeedAsync(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        // Create "Team" role
        if (!await roleManager.RoleExistsAsync("Team"))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole("Team"));
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                throw new Exception($"Failed to create Team role: {errors}");
            }
        }

        // Create test user
        var existingUser = await userManager.FindByEmailAsync("team.red@test.com");
        if (existingUser == null)
        {
            var newUser = new ApplicationUser
            {
                UserName = "team.red@test.com",  // MUST match email
                Email = "team.red@test.com",
                EmailConfirmed = true,
                ColorHex = "#FF0000",
                NumpadCode = "1234"
            };

            var createResult = await userManager.CreateAsync(newUser, "Password123!");

            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new Exception($"Failed to create test user: {errors}");
            }

            var addToRoleResult = await userManager.AddToRoleAsync(newUser, "Team");
            if (!addToRoleResult.Succeeded)
            {
                var errors = string.Join(", ", addToRoleResult.Errors.Select(e => e.Description));
                throw new Exception($"Failed to add user to Team role: {errors}");
            }
        }
    }
}
