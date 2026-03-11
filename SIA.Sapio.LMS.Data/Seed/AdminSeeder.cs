using Microsoft.AspNetCore.Identity;
using SIA.Sapio.LMS.Data.Identity;

namespace SIA.Sapio.LMS.Data.Seed
{
    public static class AdminSeeder
    {
        public static async Task SeedAdmin(UserManager<ApplicationUser> userManager)
        {
            string email = "admin@sapio.local";
            string password = "Admin123!";

            var user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = "System",
                    LastName = "Admin",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(admin, password);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
            }
        }
    }
}