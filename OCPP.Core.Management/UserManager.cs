/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2021 dallmann consulting GmbH.
 * All Rights Reserved.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using OCPP.Core.Management.Models;
using OCPP.Core.Database;

namespace OCPP.Core.Management
{
    public class UserManager
    {
        private IConfiguration Configuration;
        private ILogger<UserManager> Logger;
        private OCPPCoreContext DbContext;

        public UserManager(IConfiguration configuration, OCPPCoreContext dbContext, ILogger<UserManager> logger)
        {
            Configuration = configuration;
            DbContext = dbContext;
            Logger = logger;
        }

        public async Task<bool> SignIn(HttpContext httpContext, UserModel user, bool isPersistent = false)
        {
            try
            {
                // 1. Intentar primero login por configuración (Maestro / Break-glass)
                IEnumerable cfgUsers = Configuration.GetSection("Users").GetChildren();
                Logger.LogInformation("SignIn: Checking config for user '{0}'", user.Username);

                foreach (ConfigurationSection cfgUser in cfgUsers)
                {
                    if (cfgUser.GetValue<string>("Username") == user.Username)
                    {
                        if (cfgUser.GetValue<string>("Password") == user.Password)
                        {
                            Logger.LogInformation("SignIn: Password match in config for '{0}'", user.Username);
                            
                            try
                            {
                                var dbUserFromConfig = DbContext.Users.FirstOrDefault(u => u.Username == user.Username);
                                if (dbUserFromConfig == null)
                                {
                                    dbUserFromConfig = new User 
                                    { 
                                        Username = user.Username, 
                                        PasswordHash = user.Password,
                                        Email = user.Username + "@system.local",
                                        IsActive = true,
                                        CreateDateTime = DateTime.Now
                                    };
                                    DbContext.Users.Add(dbUserFromConfig);
                                    await DbContext.SaveChangesAsync();
                                    Logger.LogInformation("SignIn: Created config user in DB.");
                                }
                                else if (dbUserFromConfig.PasswordHash != user.Password)
                                {
                                    dbUserFromConfig.PasswordHash = user.Password;
                                    await DbContext.SaveChangesAsync();
                                    Logger.LogInformation("SignIn: Updated config user password in DB.");
                                }

                                string roleToAssign = cfgUser.GetValue<bool>("Administrator") ? Constants.AdminRoleName : Constants.OperatorRoleName;
                                var dbRole = DbContext.Roles.FirstOrDefault(r => r.Name == roleToAssign);
                                if (dbRole != null)
                                {
                                    if (!DbContext.UserRoles.Any(ur => ur.UserId == dbUserFromConfig.UserId && ur.RoleId == dbRole.RoleId))
                                    {
                                        DbContext.UserRoles.Add(new UserRole { UserId = dbUserFromConfig.UserId, RoleId = dbRole.RoleId });
                                        await DbContext.SaveChangesAsync();
                                    }
                                }

                                await PerformSignIn(httpContext, dbUserFromConfig, new List<string> { roleToAssign });
                                return true;
                            }
                            catch (Exception dbSyncEx)
                            {
                                Logger.LogError(dbSyncEx, "SignIn: Error syncing config user to DB: {0}", dbSyncEx.Message);
                                // Aún así dejamos que entre si el usuario está en config, pero usando el objeto temporal
                                await PerformSignIn(httpContext, new User { Username = user.Username }, new List<string> { Constants.AdminRoleName });
                                return true;
                            }
                        }
                        else
                        {
                            Logger.LogWarning("SignIn: Wrong password in config for '{0}'", user.Username);
                            return false;
                        }
                    }
                }

                // 2. Base de Datos
                try
                {
                    var dbUserRegular = DbContext.Users
                        .Where(u => u.Username == user.Username && u.IsActive)
                        .Select(u => new { u.UserId, u.Username, u.PasswordHash, Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList() })
                        .FirstOrDefault();

                    if (dbUserRegular != null && dbUserRegular.PasswordHash == user.Password)
                    {
                        await PerformSignIn(httpContext, new User { UserId = dbUserRegular.UserId, Username = dbUserRegular.Username }, dbUserRegular.Roles);
                        Logger.LogInformation("SignIn: Database login SUCCESS for '{0}'", user.Username);
                        return true;
                    }
                }
                catch (Exception dbEx)
                {
                    Logger.LogError(dbEx, "SignIn: Error searching user in DB: {0}", dbEx.Message);
                }
                
                Logger.LogWarning("SignIn: User '{0}' not found or invalid.", user.Username);
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SignIn: CRITICAL ERROR: {0}", ex.Message);
                throw; // Re-throw to show in controller
            }
        }

        private async Task PerformSignIn(HttpContext httpContext, User dbUser, List<string> roles)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, dbUser.UserId.ToString()),
                new Claim(ClaimTypes.Name, dbUser.Username)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            ClaimsIdentity identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);

            await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }

        public async Task SignOut(HttpContext httpContext)
        {
            await httpContext.SignOutAsync();
        }
    }
}
