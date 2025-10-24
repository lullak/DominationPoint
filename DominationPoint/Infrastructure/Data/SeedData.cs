using DominationPoint.Core.Domain;
using Microsoft.AspNetCore.Identity;

namespace DominationPoint.Infrastructure.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(typeof(SeedData).FullName ?? "SeedData");

            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                await EnsureRoleExists(roleManager, "Admin", logger);
                await EnsureRoleExists(roleManager, "Team", logger);

                var adminUser = await userManager.FindByEmailAsync("admin@dominationpoint.com");
                if (adminUser == null)
                {
                    logger.LogInformation("Admin user not found, creating a new one.");
                    adminUser = new ApplicationUser
                    {
                        UserName = "admin@dominationpoint.com",
                        Email = "admin@dominationpoint.com",
                        ColorHex = "#FFFFFF",
                        EmailConfirmed = true
                    };


                    var createUserResult = await userManager.CreateAsync(adminUser, "AdminP@ssw0rd!");
                    if (!createUserResult.Succeeded)
                    {

                        foreach (var error in createUserResult.Errors)
                        {
                            logger.LogError("Failed to create admin user: {Error}", error.Description);
                        }
                        throw new Exception("Could not create admin user.");
                    }
                    logger.LogInformation("Admin user created successfully.");

                    logger.LogInformation("Adding admin user to 'Admin' role.");
                    var addToRoleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
                    if (!addToRoleResult.Succeeded)
                    {
                        foreach (var error in addToRoleResult.Errors)
                        {
                            logger.LogError("Failed to add user to role: {Error}", error.Description);
                        }
                        throw new Exception("Could not add user to admin role.");
                    }
                    logger.LogInformation("Successfully added admin user to 'Admin' role.");
                }
                else
                {
                    logger.LogInformation("Admin user already exists.");
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static async Task EnsureRoleExists(RoleManager<IdentityRole> roleManager, string roleName, ILogger logger)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                logger.LogInformation("Role '{RoleName}' not found, creating it.", roleName);
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }
}
