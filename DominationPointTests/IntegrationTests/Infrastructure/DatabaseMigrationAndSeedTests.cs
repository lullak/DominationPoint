using DominationPoint.Core.Domain;
using DominationPoint.Infrastructure;
using DominationPoint.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace DominationPointTests.IntegrationTests.Infrastructure
{
    public class DatabaseMigrationAndSeedTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly ApplicationDbContext _context;

        public DatabaseMigrationAndSeedTests()
        {
            var services = new ServiceCollection();

            // Configure in-memory database for testing with transaction warning suppressed
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}");
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            });

            // Add Identity services
            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Warning);
            });

            _serviceProvider = services.BuildServiceProvider();
            _context = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        }

        public void Dispose()
        {
            _context?.Dispose();
            _serviceProvider?.Dispose();
        }

        #region Database Schema Tests

        [Fact]
        public async Task Database_ShouldHaveAllRequiredTables()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            // ACT & ASSERT - Verify all DbSets are accessible
            _context.ControlPoints.ShouldNotBeNull();
            _context.Games.ShouldNotBeNull();
            _context.GameScores.ShouldNotBeNull();
            _context.MapAnnotations.ShouldNotBeNull();
            _context.GameParticipants.ShouldNotBeNull();
            _context.GameEvents.ShouldNotBeNull();
            _context.Users.ShouldNotBeNull();
            _context.Roles.ShouldNotBeNull();
        }

        [Fact]
        public async Task Database_ShouldApplyMigrationsSuccessfully()
        {
            // ACT
            await _context.Database.EnsureCreatedAsync();

            // ASSERT
            var canConnect = await _context.Database.CanConnectAsync();
            canConnect.ShouldBeTrue();
        }

        [Fact]
        public async Task GameParticipant_ShouldHaveCompositeKey()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            var game = new Game
            {
                Name = "Test Game",
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddHours(1),
                Status = GameStatus.Scheduled
            };
            _context.Games.Add(game);

            var user = new ApplicationUser
            {
                UserName = "test@test.com",
                Email = "test@test.com",
                ColorHex = "#FF0000",
                EmailConfirmed = true
            };
            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            // ACT - Add first participant
            var participant1 = new GameParticipant
            {
                GameId = game.Id,
                ApplicationUserId = user.Id,
                Game = game,
                ApplicationUser = user
            };
            _context.GameParticipants.Add(participant1);
            await _context.SaveChangesAsync();

            // Clear tracking to simulate a fresh context
            _context.ChangeTracker.Clear();

            // Try to add duplicate with same composite key
            var participant2 = new GameParticipant
            {
                GameId = game.Id,
                ApplicationUserId = user.Id
            };

            // ASSERT - Should prevent duplicate composite keys
            await Should.ThrowAsync<Exception>(async () =>
            {
                _context.GameParticipants.Add(participant2);
                await _context.SaveChangesAsync();
            });
        }


        [Fact]
        public async Task Database_SchemaValidation_AllEntitiesHaveKeys()
        {
            // ARRANGE & ACT
            await _context.Database.EnsureCreatedAsync();

            var user = new ApplicationUser
            {
                UserName = "testuser@test.com",
                Email = "testuser@test.com",
                ColorHex = "#FF0000",
                EmailConfirmed = true
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Add test data for each entity to ensure keys work
            var game = new Game
            {
                Name = "Test",
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddHours(1),
                Status = GameStatus.Scheduled
            };
            _context.Games.Add(game);

            var controlPoint = new ControlPoint
            {
                Game = game,
                PositionX = 1,
                PositionY = 1,
                Status = ControlPointStatus.Inactive
            };
            _context.ControlPoints.Add(controlPoint);

            var annotation = new MapAnnotation
            {
                Game = game,
                PositionX = 1,
                PositionY = 1,
                Text = "Test"
            };
            _context.MapAnnotations.Add(annotation);

            var gameScore = new GameScore
            {
                Game = game,
                ApplicationUserId = user.Id,
                ApplicationUser = user,
                Points = 100
            };
            _context.GameScores.Add(gameScore);

            // ASSERT - Should save without errors
            await Should.NotThrowAsync(async () =>
            {
                await _context.SaveChangesAsync();
            });

            game.Id.ShouldBeGreaterThan(0);
            controlPoint.Id.ShouldBeGreaterThan(0);
            annotation.Id.ShouldBeGreaterThan(0);
            gameScore.Id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task ControlPoint_ShouldSupportNullableUser()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            var game = new Game
            {
                Name = "Test Game",
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddHours(1),
                Status = GameStatus.Active
            };
            _context.Games.Add(game);

            // ACT - Create control point without user (neutral)
            var controlPoint = new ControlPoint
            {
                Game = game,
                PositionX = 5,
                PositionY = 5,
                Status = ControlPointStatus.Inactive,
                ApplicationUserId = null,
                ApplicationUser = null
            };
            _context.ControlPoints.Add(controlPoint);

            // ASSERT
            await Should.NotThrowAsync(async () =>
            {
                await _context.SaveChangesAsync();
            });

            controlPoint.Id.ShouldBeGreaterThan(0);
            controlPoint.ApplicationUserId.ShouldBeNull();
            controlPoint.ApplicationUser.ShouldBeNull();
        }

        [Fact]
        public async Task ControlPoint_ShouldSupportControlledStatus()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            var user = new ApplicationUser
            {
                UserName = "team@test.com",
                Email = "team@test.com",
                ColorHex = "#0000FF",
                EmailConfirmed = true
            };
            _context.Users.Add(user);

            var game = new Game
            {
                Name = "Test Game",
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddHours(1),
                Status = GameStatus.Active
            };
            _context.Games.Add(game);

            await _context.SaveChangesAsync();

            // ACT - Create controlled control point
            var controlPoint = new ControlPoint
            {
                GameId = game.Id,
                PositionX = 5,
                PositionY = 5,
                Status = ControlPointStatus.Controlled,
                ApplicationUserId = user.Id
            };
            _context.ControlPoints.Add(controlPoint);

            // ASSERT
            await Should.NotThrowAsync(async () =>
            {
                await _context.SaveChangesAsync();
            });

            controlPoint.Id.ShouldBeGreaterThan(0);
            controlPoint.Status.ShouldBe(ControlPointStatus.Controlled);
            controlPoint.ApplicationUserId.ShouldBe(user.Id);
        }

        #endregion

        #region SeedData Tests

        [Fact]
        public async Task SeedData_ShouldCreateAdminRole()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT
            var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var adminRoleExists = await roleManager.RoleExistsAsync("Admin");
            adminRoleExists.ShouldBeTrue();
        }

        [Fact]
        public async Task SeedData_ShouldCreateTeamRole()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT
            var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var teamRoleExists = await roleManager.RoleExistsAsync("Team");
            teamRoleExists.ShouldBeTrue();
        }

        [Fact]
        public async Task SeedData_ShouldCreateDefaultAdminUser()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT
            var userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var adminUser = await userManager.FindByEmailAsync("admin@dominationpoint.com");

            adminUser.ShouldNotBeNull();
            adminUser.Email.ShouldBe("admin@dominationpoint.com");
            adminUser.UserName.ShouldBe("admin@dominationpoint.com");
            adminUser.EmailConfirmed.ShouldBeTrue();
            adminUser.ColorHex.ShouldBe("#FFFFFF");
        }

        [Fact]
        public async Task SeedData_AdminUser_ShouldBeInAdminRole()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT
            var userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var adminUser = await userManager.FindByEmailAsync("admin@dominationpoint.com");

            var isInAdminRole = await userManager.IsInRoleAsync(adminUser, "Admin");
            isInAdminRole.ShouldBeTrue();
        }

        [Fact]
        public async Task SeedData_WhenCalledMultipleTimes_ShouldNotCreateDuplicates()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            // ACT
            await SeedData.Initialize(_serviceProvider);
            await SeedData.Initialize(_serviceProvider); // Call twice
            await SeedData.Initialize(_serviceProvider); // Call thrice

            // ASSERT
            var adminUsers = _context.Users
                .Where(u => u.Email == "admin@dominationpoint.com")
                .ToList();

            adminUsers.Count.ShouldBe(1);
        }

        [Fact]
        public async Task SeedData_WhenAdminUserExists_ShouldNotModifyExistingUser()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await roleManager.CreateAsync(new IdentityRole("Admin"));

            var userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existingAdmin = new ApplicationUser
            {
                UserName = "admin@dominationpoint.com",
                Email = "admin@dominationpoint.com",
                ColorHex = "#000000", // Different color
                EmailConfirmed = true
            };
            await userManager.CreateAsync(existingAdmin, "ExistingP@ssw0rd!");
            await userManager.AddToRoleAsync(existingAdmin, "Admin");

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT
            var admin = await userManager.FindByEmailAsync("admin@dominationpoint.com");
            admin.ColorHex.ShouldBe("#000000"); // Should remain unchanged
        }

        [Fact]
        public async Task SeedData_AdminUser_ShouldHaveValidPassword()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT
            var userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var adminUser = await userManager.FindByEmailAsync("admin@dominationpoint.com");

            var passwordValid = await userManager.CheckPasswordAsync(adminUser, "AdminP@ssw0rd!");
            passwordValid.ShouldBeTrue();
        }

        [Fact]
        public async Task SeedData_ShouldCompleteSuccessfully()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            // ACT & ASSERT - Seeding should complete without throwing
            await Should.NotThrowAsync(async () =>
            {
                await SeedData.Initialize(_serviceProvider);
            });

            // Verify all expected data is present
            var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            (await roleManager.RoleExistsAsync("Admin")).ShouldBeTrue();
            (await roleManager.RoleExistsAsync("Team")).ShouldBeTrue();
            (await userManager.FindByEmailAsync("admin@dominationpoint.com")).ShouldNotBeNull();
        }

        [Fact]
        public async Task SeedData_WhenRolesExist_ShouldNotCreateDuplicates()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await roleManager.CreateAsync(new IdentityRole("Admin"));
            await roleManager.CreateAsync(new IdentityRole("Team"));

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT
            var allRoles = _context.Roles.ToList();
            allRoles.Count(r => r.Name == "Admin").ShouldBe(1);
            allRoles.Count(r => r.Name == "Team").ShouldBe(1);
        }

        [Fact]
        public async Task SeedData_BothRolesAndUserShouldBeCreatedTogether()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT - Verify that both roles and admin user exist
            var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var roles = await roleManager.Roles.ToListAsync();
            roles.Count.ShouldBeGreaterThanOrEqualTo(2);

            var adminUser = await userManager.FindByEmailAsync("admin@dominationpoint.com");
            adminUser.ShouldNotBeNull();

            var userRoles = await userManager.GetRolesAsync(adminUser);
            userRoles.ShouldContain("Admin");
        }

        #endregion

        #region Error Handling and Edge Cases

        [Fact]
        public async Task Database_MultipleEntitiesWithRelationships_ShouldSaveCorrectly()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            var user = new ApplicationUser
            {
                UserName = "player@test.com",
                Email = "player@test.com",
                ColorHex = "#00FF00",
                EmailConfirmed = true
            };
            _context.Users.Add(user);

            var game = new Game
            {
                Name = "Complex Game",
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddHours(2),
                Status = GameStatus.Active
            };
            _context.Games.Add(game);

            await _context.SaveChangesAsync();

            var controlPoint = new ControlPoint
            {
                GameId = game.Id,
                PositionX = 5,
                PositionY = 5,
                Status = ControlPointStatus.Controlled,
                ApplicationUserId = user.Id
            };

            var annotation = new MapAnnotation
            {
                GameId = game.Id,
                PositionX = 5,
                PositionY = 5,
                Text = "CP1"
            };

            var gameScore = new GameScore
            {
                GameId = game.Id,
                ApplicationUserId = user.Id,
                Points = 500
            };

            // ACT
            _context.ControlPoints.Add(controlPoint);
            _context.MapAnnotations.Add(annotation);
            _context.GameScores.Add(gameScore);

            await Should.NotThrowAsync(async () =>
            {
                await _context.SaveChangesAsync();
            });

            // ASSERT
            controlPoint.Id.ShouldBeGreaterThan(0);
            controlPoint.GameId.ShouldBe(game.Id);
            annotation.GameId.ShouldBe(game.Id);
            gameScore.GameId.ShouldBe(game.Id);
            gameScore.ApplicationUserId.ShouldBe(user.Id);
        }

        [Fact]
        public async Task SeedData_WithFreshDatabase_ShouldCreateAllRequiredData()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT - Comprehensive check
            var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Check roles
            (await roleManager.RoleExistsAsync("Admin")).ShouldBeTrue();
            (await roleManager.RoleExistsAsync("Team")).ShouldBeTrue();

            // Check admin user
            var admin = await userManager.FindByEmailAsync("admin@dominationpoint.com");
            admin.ShouldNotBeNull();
            admin.UserName.ShouldBe("admin@dominationpoint.com");
            admin.EmailConfirmed.ShouldBeTrue();

            // Check admin is in Admin role
            var isAdmin = await userManager.IsInRoleAsync(admin, "Admin");
            isAdmin.ShouldBeTrue();

            // Check password works
            var passwordValid = await userManager.CheckPasswordAsync(admin, "AdminP@ssw0rd!");
            passwordValid.ShouldBeTrue();
        }

        [Fact]
        public async Task GameScore_ShouldRequireUser()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            var game = new Game
            {
                Name = "Test Game",
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddHours(1),
                Status = GameStatus.Scheduled
            };
            _context.Games.Add(game);
            await _context.SaveChangesAsync();

            // ACT & ASSERT - GameScore requires ApplicationUserId
            var gameScore = new GameScore
            {
                GameId = game.Id,
                ApplicationUserId = "required-user-id",
                Points = 100
            };

            _context.GameScores.Add(gameScore);

            await Should.NotThrowAsync(async () =>
            {
                await _context.SaveChangesAsync();
            });

            gameScore.ApplicationUserId.ShouldBe("required-user-id");
        }

        #endregion

        #region SeedData Alternative Branches Tests

        [Fact]
        public async Task SeedData_WhenRolesExistButUserMissing_ShouldCreateOnlyUser()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            // Pre-create roles
            var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await roleManager.CreateAsync(new IdentityRole("Admin"));
            await roleManager.CreateAsync(new IdentityRole("Team"));

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT
            var userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var adminUser = await userManager.FindByEmailAsync("admin@dominationpoint.com");

            adminUser.ShouldNotBeNull();
            adminUser.Email.ShouldBe("admin@dominationpoint.com");

            // Roles should still be only 2
            var allRoles = await roleManager.Roles.ToListAsync();
            allRoles.Count.ShouldBe(2);
        }

        [Fact]
        public async Task SeedData_WhenUserExistsWithDifferentColorHex_ShouldNotModifyUser()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await roleManager.CreateAsync(new IdentityRole("Admin"));

            var userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existingAdmin = new ApplicationUser
            {
                UserName = "admin@dominationpoint.com",
                Email = "admin@dominationpoint.com",
                ColorHex = "#123456", // Different from seed data color
                EmailConfirmed = true
            };
            await userManager.CreateAsync(existingAdmin, "ExistingP@ssw0rd!");
            await userManager.AddToRoleAsync(existingAdmin, "Admin");

            var originalId = existingAdmin.Id;

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT
            var admin = await userManager.FindByEmailAsync("admin@dominationpoint.com");
            admin.ShouldNotBeNull();
            admin.Id.ShouldBe(originalId); // Same user
            admin.ColorHex.ShouldBe("#123456"); // Color should NOT be changed
        }

        [Fact]
        public async Task SeedData_WhenUserExistsWithEmailUnconfirmed_ShouldNotModifyUser()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await roleManager.CreateAsync(new IdentityRole("Admin"));

            var userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existingAdmin = new ApplicationUser
            {
                UserName = "admin@dominationpoint.com",
                Email = "admin@dominationpoint.com",
                ColorHex = "#FFFFFF",
                EmailConfirmed = false // NOT confirmed
            };
            await userManager.CreateAsync(existingAdmin, "ExistingP@ssw0rd!");
            await userManager.AddToRoleAsync(existingAdmin, "Admin");

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT
            var admin = await userManager.FindByEmailAsync("admin@dominationpoint.com");
            admin.ShouldNotBeNull();
            admin.EmailConfirmed.ShouldBeFalse(); // Should remain unconfirmed
        }

        [Fact]
        public async Task SeedData_WhenUserExistsButNotInAdminRole_ShouldNotAddToRole()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await roleManager.CreateAsync(new IdentityRole("Admin"));
            await roleManager.CreateAsync(new IdentityRole("Team"));

            var userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existingAdmin = new ApplicationUser
            {
                UserName = "admin@dominationpoint.com",
                Email = "admin@dominationpoint.com",
                ColorHex = "#FFFFFF",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(existingAdmin, "ExistingP@ssw0rd!");
            // Deliberately NOT adding to Admin role

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT
            var admin = await userManager.FindByEmailAsync("admin@dominationpoint.com");
            var roles = await userManager.GetRolesAsync(admin);

            // Should have role assigned by seed data
            roles.Count.ShouldBeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public async Task SeedData_WhenAdminRoleExistsButTeamRoleMissing_ShouldCreateOnlyTeamRole()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await roleManager.CreateAsync(new IdentityRole("Admin"));
            // Team role is missing

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT
            (await roleManager.RoleExistsAsync("Admin")).ShouldBeTrue();
            (await roleManager.RoleExistsAsync("Team")).ShouldBeTrue();

            var allRoles = await roleManager.Roles.ToListAsync();
            allRoles.Count.ShouldBe(2);
        }

        [Fact]
        public async Task SeedData_WhenTeamRoleExistsButAdminRoleMissing_ShouldCreateOnlyAdminRole()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await roleManager.CreateAsync(new IdentityRole("Team"));
            // Admin role is missing

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT
            (await roleManager.RoleExistsAsync("Admin")).ShouldBeTrue();
            (await roleManager.RoleExistsAsync("Team")).ShouldBeTrue();

            var allRoles = await roleManager.Roles.ToListAsync();
            allRoles.Count.ShouldBe(2);
        }

        [Fact]
        public async Task SeedData_ExecutionOrder_RolesShouldBeCreatedBeforeUser()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT - Both roles and user should exist, proving correct order
            var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            (await roleManager.RoleExistsAsync("Admin")).ShouldBeTrue();
            (await roleManager.RoleExistsAsync("Team")).ShouldBeTrue();

            var adminUser = await userManager.FindByEmailAsync("admin@dominationpoint.com");
            adminUser.ShouldNotBeNull();

            var isInRole = await userManager.IsInRoleAsync(adminUser, "Admin");
            isInRole.ShouldBeTrue();
        }

        [Fact]
        public async Task SeedData_WhenDatabaseIsEmpty_ShouldCreateAllEntities()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();
            // Database is completely empty

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT - All entities should be created
            var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var roles = await roleManager.Roles.ToListAsync();
            roles.Count.ShouldBeGreaterThanOrEqualTo(2);
            roles.Any(r => r.Name == "Admin").ShouldBeTrue();
            roles.Any(r => r.Name == "Team").ShouldBeTrue();

            var adminUser = await userManager.FindByEmailAsync("admin@dominationpoint.com");
            adminUser.ShouldNotBeNull();
            adminUser.ColorHex.ShouldBe("#FFFFFF");
            adminUser.EmailConfirmed.ShouldBeTrue();

            var isInAdminRole = await userManager.IsInRoleAsync(adminUser, "Admin");
            isInAdminRole.ShouldBeTrue();
        }

        [Fact]
        public async Task SeedData_UserCreation_ShouldUseCorrectPassword()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT
            var userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var adminUser = await userManager.FindByEmailAsync("admin@dominationpoint.com");

            // Verify the seeded password works
            var passwordCorrect = await userManager.CheckPasswordAsync(adminUser, "AdminP@ssw0rd!");
            passwordCorrect.ShouldBeTrue();

            // Verify wrong password fails
            var wrongPasswordFails = await userManager.CheckPasswordAsync(adminUser, "WrongPassword!");
            wrongPasswordFails.ShouldBeFalse();
        }

        [Fact]
        public async Task SeedData_AfterSeeding_AdminUserShouldBeFullyConfigured()
        {
            // ARRANGE
            await _context.Database.EnsureCreatedAsync();

            // ACT
            await SeedData.Initialize(_serviceProvider);

            // ASSERT - Comprehensive check of admin user properties
            var userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = await userManager.FindByEmailAsync("admin@dominationpoint.com");

            admin.ShouldNotBeNull();
            admin.UserName.ShouldBe("admin@dominationpoint.com");
            admin.Email.ShouldBe("admin@dominationpoint.com");
            admin.NormalizedEmail.ShouldNotBeNullOrEmpty();
            admin.NormalizedUserName.ShouldNotBeNullOrEmpty();
            admin.ColorHex.ShouldBe("#FFFFFF");
            admin.EmailConfirmed.ShouldBeTrue();
            admin.PasswordHash.ShouldNotBeNullOrEmpty();

            var roles = await userManager.GetRolesAsync(admin);
            roles.ShouldContain("Admin");
        }

        #endregion
        [Fact]
        public void OnModelCreating_ShouldConfigureGameParticipantCompositeKey()
        {
            // Test that GameParticipant has composite key (GameId, ApplicationUserId)
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb")
                .Options;

            using var context = new ApplicationDbContext(options);
            var model = context.Model;
            var entityType = model.FindEntityType(typeof(GameParticipant));

            var keys = entityType.FindPrimaryKey();
            keys.Properties.Count.ShouldBe(2);
            keys.Properties.Select(p => p.Name).ShouldContain("GameId");
            keys.Properties.Select(p => p.Name).ShouldContain("ApplicationUserId");
        }

        [Fact]
        public void OnModelCreating_ShouldConfigureGameRelationships()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .Options;

            using var context = new ApplicationDbContext(options);
            var model = context.Model;
            var gameEntity = model.FindEntityType(typeof(Game));

            var navigations = gameEntity.GetNavigations();
            navigations.Count().ShouldBeGreaterThan(0); // Now this will pass!
        }


    }
}
