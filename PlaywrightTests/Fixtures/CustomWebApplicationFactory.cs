using DominationPoint.Infrastructure;
using DominationPoint.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DominationPoint.PlaywrightTests.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _url;

    public CustomWebApplicationFactory(string url)
    {
        _url = url;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseUrls(_url);
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext configuration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for tests
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite("DataSource=:memory:");
                options.EnableSensitiveDataLogging();
            });

            // Build service provider and ensure database is created
            var sp = services.BuildServiceProvider();

            using (var scope = sp.CreateScope())
            {
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<ApplicationDbContext>();

                // Ensure database is created
                db.Database.OpenConnection(); // Important for SQLite in-memory
                db.Database.EnsureCreated();

                // Seed data if needed
                try
                {
                    DominationPoint.Infrastructure.Data.SeedData.Initialize(scopedServices).Wait();
                }
                catch
                {
                    // Seed might fail if already initialized, that's okay
                }
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up the database connection
            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Database.CloseConnection();
            }
        }
        base.Dispose(disposing);
    }
}
