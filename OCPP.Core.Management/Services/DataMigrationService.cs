using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;

namespace OCPP.Core.Management.Services
{
    public class DataMigrationService
    {
        private readonly OCPPCoreContext _localDb;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DataMigrationService> _logger;

        public DataMigrationService(
            OCPPCoreContext localDb,
            IConfiguration configuration,
            ILogger<DataMigrationService> logger)
        {
            _localDb = localDb;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<(bool success, string message)> SyncFromProduction()
        {
            try
            {
                string prodConnectionString = _configuration.GetConnectionString("SqlServer");
                if (string.IsNullOrEmpty(prodConnectionString))
                {
                    return (false, "No se encontró la cadena de conexión 'SqlServer' en la configuración.");
                }

                var optionsBuilder = new DbContextOptionsBuilder<OCPPCoreContext>();
                optionsBuilder.UseSqlServer(prodConnectionString);

                using var prodDb = new OCPPCoreContext(optionsBuilder.Options);
                prodDb.Database.SetCommandTimeout(60); // Aumentar timeout a 60 segundos

                _logger.LogInformation("Iniciando migración desde Producción...");

                // 0. Probar conexión rápidamente
                try 
                {
                    _logger.LogInformation("Probando conexión con el servidor de producción...");
                    var canConnect = await prodDb.Database.CanConnectAsync();
                    if (!canConnect)
                    {
                        return (false, "No se pudo establecer conexión con el servidor de producción. Verifica que el servidor 'sol050' sea accesible desde tu red interna (VPN o red de la oficina).");
                    }
                }
                catch (Exception ex)
                {
                    return (false, $"Error al intentar contactar el servidor de producción: {ex.Message}");
                }

                // 1. Limpiar datos locales usando SQL crudo
                _logger.LogInformation("Limpiando base de datos local...");
                
                await _localDb.Database.OpenConnectionAsync();
                using var transaction = await _localDb.Database.BeginTransactionAsync();

                string currentTable = "Inicio";
                try 
                {
                    if (_localDb.Database.IsSqlite())
                    {
                        await _localDb.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
                    }

                    var tables = new[] { 
                        "RolePermissions", "UserRoles", "UserChargePoints", "ChargeTags", 
                        "Transactions", "ConnectorStatus", "ChargePoint", "Customers", 
                        "Users", "Roles", "Permissions", "SystemSetting", "MessageLog" 
                    };

                    foreach (var table in tables)
                    {
                        try 
                        {
                            await _localDb.Database.ExecuteSqlRawAsync($"DELETE FROM [{table}];");
                        }
                        catch (Exception tableDeleteEx)
                        {
                            _logger.LogWarning($"No se pudo limpiar la tabla {table}: {tableDeleteEx.Message}");
                        }
                    }

                    _localDb.ChangeTracker.Clear();

                    // 2. Migrar Tablas en orden
                    currentTable = "Permisos";
                    _logger.LogInformation($"Migrando {currentTable}...");
                    var prodPermissions = await prodDb.Permissions.AsNoTracking().ToListAsync();
                    _localDb.ChangeTracker.Clear();
                    _localDb.Permissions.AddRange(prodPermissions);
                    try { await _localDb.SaveChangesAsync(); } catch (Exception ex) { throw new Exception($"Error al guardar {currentTable}: {ex.Message}", ex); }

                    currentTable = "Roles";
                    _logger.LogInformation($"Migrando {currentTable}...");
                    var prodRoles = await prodDb.Roles.AsNoTracking().ToListAsync();
                    _localDb.ChangeTracker.Clear();
                    _localDb.Roles.AddRange(prodRoles);
                    try { await _localDb.SaveChangesAsync(); } catch (Exception ex) { throw new Exception($"Error al guardar {currentTable}: {ex.Message}", ex); }

                    currentTable = "RolePermissions";
                    _logger.LogInformation($"Migrando {currentTable}...");
                    var prodRolePerms = await prodDb.RolePermissions.AsNoTracking().ToListAsync();
                    
                    var vRoleIds = await _localDb.Roles.Select(r => r.RoleId).ToListAsync();
                    var vPermIds = await _localDb.Permissions.Select(p => p.PermissionId).ToListAsync();
                    var cleanRolePerms = prodRolePerms.Where(rp => vRoleIds.Contains(rp.RoleId) && vPermIds.Contains(rp.PermissionId)).ToList();
                    
                    _localDb.ChangeTracker.Clear();
                    _localDb.RolePermissions.AddRange(cleanRolePerms);
                    try { await _localDb.SaveChangesAsync(); } catch (Exception ex) { throw new Exception($"Error al guardar {currentTable}: {ex.Message}", ex); }

                    currentTable = "Usuarios";
                    _logger.LogInformation($"Migrando {currentTable}...");
                    var prodUsers = await prodDb.Users.AsNoTracking().ToListAsync();
                    _localDb.ChangeTracker.Clear();
                    _localDb.Users.AddRange(prodUsers);
                    try { await _localDb.SaveChangesAsync(); } catch (Exception ex) { throw new Exception($"Error al guardar {currentTable}: {ex.Message}", ex); }

                    currentTable = "UserRoles";
                    _logger.LogInformation($"Migrando {currentTable}...");
                    var prodUserRoles = await prodDb.UserRoles.AsNoTracking().ToListAsync();
                    
                    // Filtrar huérfanos para evitar errores de llave foránea
                    var validUserIds = await _localDb.Users.Select(u => u.UserId).ToListAsync();
                    var validRoleIds = await _localDb.Roles.Select(r => r.RoleId).ToListAsync();
                    var cleanUserRoles = prodUserRoles.Where(ur => validUserIds.Contains(ur.UserId) && validRoleIds.Contains(ur.RoleId)).ToList();
                    
                    if (cleanUserRoles.Count < prodUserRoles.Count)
                    {
                        _logger.LogWarning($"Se filtraron {prodUserRoles.Count - cleanUserRoles.Count} UserRoles huérfanos.");
                    }

                    _localDb.ChangeTracker.Clear();
                    _localDb.UserRoles.AddRange(cleanUserRoles);
                    try { await _localDb.SaveChangesAsync(); } catch (Exception ex) { throw new Exception($"Error al guardar {currentTable}: {ex.Message}", ex); }

                    currentTable = "Clientes";
                    _logger.LogInformation($"Migrando {currentTable}...");
                    var prodCustomers = await prodDb.Customers.AsNoTracking().ToListAsync();
                    _localDb.ChangeTracker.Clear();
                    _localDb.Customers.AddRange(prodCustomers);
                    try { await _localDb.SaveChangesAsync(); } catch (Exception ex) { throw new Exception($"Error al guardar {currentTable}: {ex.Message}", ex); }

                    currentTable = "Cargadores";
                    _logger.LogInformation($"Migrando {currentTable}...");
                    var prodCPs = await prodDb.ChargePoints.AsNoTracking().ToListAsync();
                    _localDb.ChangeTracker.Clear();
                    _localDb.ChargePoints.AddRange(prodCPs);
                    try { await _localDb.SaveChangesAsync(); } catch (Exception ex) { throw new Exception($"Error al guardar {currentTable}: {ex.Message}", ex); }

                    currentTable = "Tags";
                    _logger.LogInformation($"Migrando {currentTable}...");
                    var prodTags = await prodDb.ChargeTags.AsNoTracking().ToListAsync();
                    
                    var validCustIds = await _localDb.Customers.Select(c => c.CustomerId).ToListAsync();
                    foreach (var tag in prodTags)
                    {
                        if (tag.CustomerId.HasValue && !validCustIds.Contains(tag.CustomerId.Value))
                        {
                            tag.CustomerId = null; // Evitar error si el cliente no existe localmente
                        }
                    }

                    _localDb.ChangeTracker.Clear();
                    _localDb.ChargeTags.AddRange(prodTags);
                    try { await _localDb.SaveChangesAsync(); } catch (Exception ex) { throw new Exception($"Error al guardar {currentTable}: {ex.Message}", ex); }

                    currentTable = "UserChargePoints";
                    _logger.LogInformation($"Migrando {currentTable}...");
                    var prodUserCPs = await prodDb.UserChargePoints.AsNoTracking().ToListAsync();
                    
                    var vUIds = await _localDb.Users.Select(u => u.UserId).ToListAsync();
                    var vCPIds = await _localDb.ChargePoints.Select(cp => cp.ChargePointId).ToListAsync();
                    var cleanUserCPs = prodUserCPs.Where(ucp => vUIds.Contains(ucp.UserId) && vCPIds.Contains(ucp.ChargePointId)).ToList();
                    
                    _localDb.ChangeTracker.Clear();
                    _localDb.UserChargePoints.AddRange(cleanUserCPs);
                    try { await _localDb.SaveChangesAsync(); } catch (Exception ex) { throw new Exception($"Error al guardar {currentTable}: {ex.Message}", ex); }
                    
                    currentTable = "Ajustes";
                    _logger.LogInformation($"Migrando {currentTable}...");
                    var prodSettings = await prodDb.SystemSettings.AsNoTracking().ToListAsync();
                    _localDb.ChangeTracker.Clear();
                    foreach (var setting in prodSettings)
                    {
                        if (!await _localDb.SystemSettings.AnyAsync(s => s.SettingId == setting.SettingId))
                        {
                            _localDb.SystemSettings.Add(setting);
                        }
                    }
                    try { await _localDb.SaveChangesAsync(); } catch (Exception ex) { throw new Exception($"Error al guardar {currentTable}: {ex.Message}", ex); }

                    await transaction.CommitAsync();

                    if (_localDb.Database.IsSqlite())
                    {
                        await _localDb.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
                    }
                }
                catch (Exception tableEx)
                {
                    await transaction.RollbackAsync();
                    
                    string detailedError = GetFullExceptionMessage(tableEx);
                    _logger.LogError(tableEx, $"Error durante la migración en la tabla {currentTable}. Detalle: {detailedError}");
                    return (false, $"Error durante la migración en [{currentTable}]: {detailedError}");
                }
                finally 
                {
                    await _localDb.Database.CloseConnectionAsync();
                }

                _logger.LogInformation("Migración completada con éxito.");
                return (true, "Base de datos local sincronizada con éxito desde producción.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico durante la migración de datos.");
                return (false, $"Error crítico: {GetFullExceptionMessage(ex)}");
            }
        }

        public async Task EnsureDefaultPermissions()
        {
            var defaultPermissions = new List<Permission>
            {
                new Permission { Name = "Ver Dashboard", Controller = "Home", Action = "Index", Description = "Acceso a la vista principal con estadísticas en tiempo real." },
                new Permission { Name = "Control de Conectores", Controller = "Home", Action = "Control", Description = "Permite iniciar/detener cargas remotamente y ver detalles técnicos del conector." },
                new Permission { Name = "Gestión de Usuarios", Controller = "Users", Action = "Index", Description = "Permite crear, editar y eliminar usuarios del sistema." },
                new Permission { Name = "Gestión de Roles", Controller = "Roles", Action = "Index", Description = "Permite crear roles y asignar permisos a los mismos." },
                new Permission { Name = "Lista de Cargadores", Controller = "Home", Action = "ChargePoint", Description = "Ver listado y estado de todos los cargadores conectados." },
                new Permission { Name = "Gestión de Clientes", Controller = "Customers", Action = "Index", Description = "Administración de clientes y sus Tags RFID." },
                new Permission { Name = "Historial de Transacciones", Controller = "Home", Action = "Transactions", Description = "Ver historial completo de cargas realizadas." },
                new Permission { Name = "Configuración del Sistema", Controller = "Settings", Action = "Index", Description = "Acceso a configuraciones globales (precios, nombre de empresa, etc)." },
                new Permission { Name = "Reportes", Controller = "Home", Action = "ChargeReport", Description = "Generación y exportación de reportes de consumo." },
                new Permission { Name = "Registro de Eventos", Controller = "Home", Action = "SystemEvents", Description = "Auditoría de eventos y errores del sistema." },
                new Permission { Name = "Carga Rápida", Controller = "Home", Action = "QuickStart", Description = "Interfaz simplificada para iniciar cargas manuales rápidamente." },
                new Permission { Name = "Vista de Conectores", Controller = "Home", Action = "Connector", Description = "Configuración y estado detallado de cada conector." },
                new Permission { Name = "Administración de Tags", Controller = "Home", Action = "ChargeTag", Description = "Gestión de tarjetas RFID y tokens de autenticación." },
                new Permission { Name = "Diagnósticos", Controller = "Home", Action = "Diagnostics", Description = "Herramientas técnicas y visualización de logs." },
            };

            foreach (var perm in defaultPermissions)
            {
                if (!await _localDb.Permissions.AnyAsync(p => p.Controller == perm.Controller && p.Action == perm.Action))
                {
                    _localDb.Permissions.Add(perm);
                }
                else 
                {
                    // Update description if it exists but is empty
                    var existing = await _localDb.Permissions.FirstOrDefaultAsync(p => p.Controller == perm.Controller && p.Action == perm.Action);
                    if (existing != null && string.IsNullOrEmpty(existing.Description))
                    {
                        existing.Description = perm.Description;
                        existing.Name = perm.Name; // Ensure nice name too
                    }
                }
            }
            await _localDb.SaveChangesAsync();
        }

        private string GetFullExceptionMessage(Exception ex)
        {
            if (ex == null) return "Unknown error";
            var msg = ex.Message;
            if (ex.InnerException != null)
            {
                msg += " ---> " + GetFullExceptionMessage(ex.InnerException);
            }
            return msg;
        }
    }
}
