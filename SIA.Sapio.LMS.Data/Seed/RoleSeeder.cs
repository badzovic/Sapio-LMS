using Microsoft.AspNetCore.Identity;
using SIA.Sapio.LMS.Data.Seed;

namespace SIA.Sapio.LMS.Data.Seed
{
    public static class RoleSeeder
    {
        public static async Task SeedRoles(RoleManager<IdentityRole> roleManager)
        {
            string[] roles = { "Admin", /*"Instructor"*/ "Student" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }
    }
}