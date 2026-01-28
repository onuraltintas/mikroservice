using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Seed;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IdentityDbContext>>();

        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<EduPlatform.Shared.Security.Interfaces.IPasswordHasher>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        try
        {
            logger.LogInformation("🌱 Seeding Starting...");

            // 1. Seed Roles (Dynamic)
            if (!await context.Roles.AnyAsync())
            {
                logger.LogInformation("Creating default roles...");
                var roles = Enum.GetValues<Identity.Domain.Enums.UserRole>()
                    .Select(r => Role.Create(r.ToString(), $"Default role for {r}", isSystemRole: true))
                    .ToList();

                await context.Roles.AddRangeAsync(roles);
                await context.SaveChangesAsync();
                logger.LogInformation("✅ Roles created.");
            }
            
            // Re-fetch roles to get Ids
            var dbRoles = await context.Roles.ToListAsync();

            // 1.1 Seed Permissions Table
            var allPermissions = Identity.Domain.Constants.Permissions.GetAll();
            var existingDbPermissions = await context.Permissions.Select(p => p.Key).ToListAsync();
            var newDbPermissions = allPermissions.Except(existingDbPermissions).ToList();

            if (newDbPermissions.Any())
            {
                logger.LogInformation($"Seeding {newDbPermissions.Count} new permissions into Permissions table...");
                foreach (var permKey in newDbPermissions)
                {
                    // Parse Group from Key (e.g. Permissions.Users.View -> Group: Users)
                    var parts = permKey.Split('.');
                    var group = parts.Length > 2 ? parts[1] : "General";
                    var description = $"System permission for {permKey}";

                    var newPerm = Permission.Create(permKey, description, group, isSystem: true);
                    context.Permissions.Add(newPerm);
                }
                await context.SaveChangesAsync();
                logger.LogInformation("✅ Permissions table seeded.");
            }

            // 1.2 Seed Role Permissions
            var institutionPermissions = new List<string> 
            { 
                Identity.Domain.Constants.Permissions.Users.View,
                Identity.Domain.Constants.Permissions.Users.Create,
                Identity.Domain.Constants.Permissions.Users.Edit,
                Identity.Domain.Constants.Permissions.Users.Delete
            };
            
            var rolePermissionsMap = new Dictionary<string, List<string>>
            {
                { Identity.Domain.Enums.UserRole.SystemAdmin.ToString(), allPermissions },
                { Identity.Domain.Enums.UserRole.InstitutionOwner.ToString(), institutionPermissions },
                { Identity.Domain.Enums.UserRole.InstitutionAdmin.ToString(), institutionPermissions } // Simplify: same for now
            };

            foreach (var roleName in rolePermissionsMap.Keys)
            {
                var role = dbRoles.FirstOrDefault(r => r.Name == roleName);
                if (role != null)
                {
                    var permsToAssign = rolePermissionsMap[roleName];
                    var existingPerms = await context.RolePermissions
                        .Where(p => p.RoleId == role.Id)
                        .Select(p => p.Permission)
                        .ToListAsync();

                    var newPerms = permsToAssign.Except(existingPerms).ToList();
                    
                    if (newPerms.Any())
                    {
                        logger.LogInformation($"Adding {newPerms.Count} permissions to {roleName}...");
                        foreach (var p in newPerms)
                        {
                            context.RolePermissions.Add(new RolePermission(role.Id, p));
                        }
                    }
                }
            }
            await context.SaveChangesAsync();
            logger.LogInformation("✅ Permissions seeded for SystemAdmin/Institution roles.");

            // 2. Check if admin user exists in DB
            var adminEmail = "admin@edu.com";
            var existingUser = await userRepository.GetByEmailAsync(adminEmail, CancellationToken.None);

            if (existingUser != null)
            {
                logger.LogInformation("✅ Database already seeded.");
                return;
            }

            logger.LogInformation("⚡ No users found. Starting seed process...");

            // Get Passwords from Env
            var adminPass = configuration["TEST_ADMIN_PASSWORD"] ?? "VForVan_40!";
            var defaultPass = configuration["TEST_DEFAULT_PASSWORD"] ?? "VForVan_40!";

            // 3. Define Users to Seed
            var usersToSeed = new[]
            {
                new { Email = "admin@edu.com", Pass = adminPass, First = "System", Last = "Admin", RoleName = Identity.Domain.Enums.UserRole.SystemAdmin.ToString() },
                new { Email = "teacher@edu.com", Pass = defaultPass, First = "Demo", Last = "Teacher", RoleName = Identity.Domain.Enums.UserRole.Teacher.ToString() },
                new { Email = "student@edu.com", Pass = defaultPass, First = "Demo", Last = "Student", RoleName = Identity.Domain.Enums.UserRole.Student.ToString() },
                new { Email = "institution@edu.com", Pass = defaultPass, First = "Kurum", Last = "Yöneticisi", RoleName = Identity.Domain.Enums.UserRole.InstitutionOwner.ToString() },
                new { Email = "parent@edu.com", Pass = defaultPass, First = "Demo", Last = "Parent", RoleName = Identity.Domain.Enums.UserRole.Parent.ToString() },
                new { Email = "manager@edu.com", Pass = defaultPass, First = "Kurum", Last = "Müdürü", RoleName = Identity.Domain.Enums.UserRole.InstitutionAdmin.ToString() }
            };

            foreach (var userData in usersToSeed)
            {
                try
                {
                    logger.LogInformation($"Creating user: {userData.Email}...");
                    
                    var userId = Guid.NewGuid();

                    var newUser = User.Create(
                        userId,
                        userData.Email,
                        userData.First,
                        userData.Last
                    );

                    passwordHasher.CreatePasswordHash(userData.Pass, out byte[] passwordHash, out byte[] passwordSalt);
                    newUser.SetPassword(passwordHash, passwordSalt);

                    // Find Role ID
                    var roleEntity = dbRoles.FirstOrDefault(r => r.Name == userData.RoleName);
                    if (roleEntity == null) throw new Exception($"Role {userData.RoleName} not found in DB.");

                    newUser.AddRole(new UserRole(newUser.Id, roleEntity.Id));

                    // If Institution Owner, create Institution
                    if (userData.RoleName == Identity.Domain.Enums.UserRole.InstitutionOwner.ToString())
                    {
                         var institution = Institution.Create(
                             "Atlas Okulları",
                             Identity.Domain.Enums.InstitutionType.School 
                         );
                         
                         var instRepo = scope.ServiceProvider.GetRequiredService<IInstitutionRepository>();
                         await instRepo.AddAsync(institution, CancellationToken.None);
                         
                         var adminRel = InstitutionAdmin.Create(
                             newUser.Id,
                             institution.Id,
                             Identity.Domain.Enums.InstitutionAdminRole.Admin
                         );
                         await instRepo.AddAdminAsync(adminRel, CancellationToken.None);
                    }

                    await userRepository.AddAsync(newUser, CancellationToken.None);
                    await unitOfWork.SaveChangesAsync(CancellationToken.None);
                    
                    logger.LogInformation($"  -> Local DB user created.");

                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Error seeding user {userData.Email}");
                }
            }

            logger.LogInformation("✅ Seeding Completed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Seeding Failed.");
        }
    }
}
