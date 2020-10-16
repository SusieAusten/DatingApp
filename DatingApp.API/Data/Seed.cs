using System.Collections.Generic;
using System.Linq;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace DatingApp.API.Data
{
    public class Seed
    {
        public static void SeedUsers(UserManager<User> userManager, RoleManager<Role> roleManager)
        {
            if (!userManager.Users.Any())
            {
                var userData = System.IO.File.ReadAllText("Data/UserSeedData.json");
                var users = JsonConvert.DeserializeObject<List<User>>(userData);

                //create some roles
                var roles = new List<Role>
                {
                    new Role{Name = "Member"},
                    new Role{Name = "Admin"},
                    new Role{Name = "Moderator"},
                    new Role{Name = "VIP"}
                };

                foreach (var role in roles)
                {
                    roleManager.CreateAsync(role).Wait();
                }

                //create admin user
                var adminUser = new User
                {
                    UserName = "AdminJC"
                };

                // userManager.CreateAsync(adminUser, "password").Wait();
                // userManager.AddToRoleAsync(adminUser, "Admin");

                //userManager.AddToRolesAsync(adminUser, new[] {"Admin", "Moderator"});

                var adminResult = userManager.CreateAsync(adminUser, "PasswordJC").Result;

                if (adminResult.Succeeded)
                {
                    var admin = userManager.FindByNameAsync("AdminJC").Result;
                    userManager.AddToRolesAsync(admin, new[] {"Admin", "Moderator"});

                }

                //create the users

                foreach (var user in users)
                {
                    user.Photos.FirstOrDefault().IsApproved = true;

                    // userManager.CreateAsync(user, "password").Wait();
                    // userManager.AddToRoleAsync(user, "Member");
                    var result = userManager.CreateAsync(user, "password").Result;
                    if (result.Succeeded)
                    {
                        userManager.AddToRoleAsync(user, "Member");
                    }

                }
            }
        }

        private static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using (var hmac = new System.Security.Cryptography.HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            }

        }
    }
}