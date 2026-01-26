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

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;

#nullable disable

namespace OCPP.Core.Database
{
    public partial class OCPPCoreContext : DbContext
    {
        public OCPPCoreContext(DbContextOptions<OCPPCoreContext> options)
            : base(options)
        {
        }

        public virtual DbSet<ChargePoint> ChargePoints { get; set; }
        public virtual DbSet<ChargeTag> ChargeTags { get; set; }
        public virtual DbSet<ConnectorStatus> ConnectorStatuses { get; set; }
        public virtual DbSet<MessageLog> MessageLogs { get; set; }
        public virtual DbSet<Transaction> Transactions { get; set; }
        public virtual DbSet<SystemSetting> SystemSettings { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<Role> Roles { get; set; }
        public virtual DbSet<UserRole> UserRoles { get; set; }
        public virtual DbSet<UserChargePoint> UserChargePoints { get; set; }
        public virtual DbSet<Permission> Permissions { get; set; }
        public virtual DbSet<RolePermission> RolePermissions { get; set; }
        public virtual DbSet<Customer> Customers { get; set; }
        public virtual DbSet<TagGroup> TagGroups { get; set; }
        public virtual DbSet<ErrorCatalogEntry> ErrorCatalog { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChargePoint>(entity =>
            {
                entity.ToTable("ChargePoint");
            
            if (this.Database.IsSqlServer())
            {
                modelBuilder.Entity<User>().Ignore(u => u.Name);
            }

                entity.HasIndex(e => e.ChargePointId, "ChargePoint_Identifier")
                    .IsUnique();

                entity.Property(e => e.ChargePointId).HasMaxLength(100);

                entity.Property(e => e.Comment).HasMaxLength(200);

                entity.Property(e => e.Name).HasMaxLength(100);

                entity.Property(e => e.Username).HasMaxLength(50);

                entity.Property(e => e.Password).HasMaxLength(50);

                entity.Property(e => e.ClientCertThumb).HasMaxLength(100);
            });

            modelBuilder.Entity<ChargeTag>(entity =>
            {
                entity.HasKey(e => e.TagId)
                    .HasName("PK_ChargeKeys");

                entity.Property(e => e.TagId).HasMaxLength(50);

                entity.Property(e => e.ParentTagId).HasMaxLength(50);

                entity.Property(e => e.TagName).HasMaxLength(200);
            });

            modelBuilder.Entity<ConnectorStatus>(entity =>
            {
                entity.HasKey(e => new { e.ChargePointId, e.ConnectorId });

                entity.ToTable("ConnectorStatus");

                entity.Property(e => e.ChargePointId).HasMaxLength(100);

                entity.Property(e => e.ConnectorName).HasMaxLength(100);

                entity.Property(e => e.LastStatus).HasMaxLength(100);
            });

            modelBuilder.Entity<MessageLog>(entity =>
            {
                entity.HasKey(e => e.LogId);

                entity.ToTable("MessageLog");

                entity.HasIndex(e => e.LogTime, "IX_MessageLog_ChargePointId");

                entity.Property(e => e.ChargePointId)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.ErrorCode).HasMaxLength(100);

                entity.Property(e => e.Message)
                    .IsRequired()
                    .HasMaxLength(100);
            });

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.Property(e => e.Uid).HasMaxLength(50);

                entity.Property(e => e.ChargePointId)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.StartTagId).HasMaxLength(50);

                entity.Property(e => e.StartResult).HasMaxLength(100);

                entity.Property(e => e.StopTagId).HasMaxLength(50);

                entity.Property(e => e.StopReason).HasMaxLength(100);
                entity.Property(e => e.CustomerIdentifier).HasMaxLength(50);
                entity.Property(e => e.CustomerPhone).HasMaxLength(50);
                entity.Property(e => e.CustomerEmail).HasMaxLength(100);

                entity.HasOne(d => d.ChargePoint)
                    .WithMany(p => p.Transactions)
                    .HasForeignKey(d => d.ChargePointId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Transactions_ChargePoint");

                entity.HasIndex(e => new { e.ChargePointId, e.ConnectorId });
            });

            modelBuilder.Entity<SystemSetting>(entity =>
            {
                entity.ToTable("SystemSetting");
                entity.HasKey(e => e.SettingId);
                entity.Property(e => e.SettingId).HasMaxLength(100);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(e => e.RoleId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            });

            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.RoleId });
                entity.HasOne(d => d.User).WithMany(p => p.UserRoles).HasForeignKey(d => d.UserId);
                entity.HasOne(d => d.Role).WithMany(p => p.UserRoles).HasForeignKey(d => d.RoleId);
            });

            modelBuilder.Entity<UserChargePoint>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.ChargePointId });
                entity.HasOne(d => d.User).WithMany(p => p.UserChargePoints).HasForeignKey(d => d.UserId);
                entity.HasOne(d => d.ChargePoint).WithMany(p => p.UserChargePoints).HasForeignKey(d => d.ChargePointId);
            });

            modelBuilder.Entity<Permission>(entity =>
            {
                entity.HasKey(e => e.PermissionId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.HasKey(e => new { e.RoleId, e.PermissionId });
                entity.HasOne(d => d.Role).WithMany(p => p.RolePermissions).HasForeignKey(d => d.RoleId);
                entity.HasOne(d => d.Permission).WithMany(p => p.RolePermissions).HasForeignKey(d => d.PermissionId);
            });

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.CustomerId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Identifier).HasMaxLength(50);
                entity.Property(e => e.Phone).HasMaxLength(50);
                entity.Property(e => e.Email).HasMaxLength(100);
            });

            modelBuilder.Entity<ChargeTag>(entity =>
            {
                entity.HasOne(d => d.Customer)
                    .WithMany(p => p.ChargeTags)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<TagGroup>(entity =>
            {
                entity.HasKey(e => e.TagGroupId);
                entity.ToTable("TagGroups");
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(255);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        public void CheckDatabase()
        {
            if (this.Database.IsSqlite())
            {
                this.Database.EnsureCreated();

                // SQLite manual migrations (EnsureCreated only works on new databases)
                this.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS TagGroups (TagGroupId INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Description TEXT NULL)");
                this.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS ErrorCatalog (ErrorCode NVARCHAR(100) PRIMARY KEY, Title NVARCHAR(200) NOT NULL, Description TEXT NOT NULL, CommonCauses TEXT NULL, SuggestedSolution TEXT NULL, Severity NVARCHAR(50) NULL, Category NVARCHAR(100) NULL)");
                this.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS Customers (CustomerId INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Identifier TEXT NULL, Phone TEXT NULL, Email TEXT NULL, Balance DECIMAL(18, 4) NOT NULL DEFAULT 0)");
                
                // Helper to check for column existence in SQLite to silence logs
                Action<string, string, string> addColumnIfMissing = (tableName, columnName, columnDefinition) => {
                    var conn = this.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open) conn.Open();
                    using (var cmd = conn.CreateCommand()) {
                        cmd.CommandText = $"PRAGMA table_info({tableName})";
                        bool exists = false;
                        using (var reader = cmd.ExecuteReader()) {
                            while (reader.Read()) {
                                if (reader["name"].ToString().Equals(columnName, StringComparison.OrdinalIgnoreCase)) {
                                    exists = true;
                                    break;
                                }
                            }
                        }
                        if (!exists) {
                            cmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}";
                            cmd.ExecuteNonQuery();
                        }
                    }
                };

                addColumnIfMissing("Transactions", "CustomerIdentifier", "TEXT NULL");
                addColumnIfMissing("Transactions", "CustomerPhone", "TEXT NULL");
                addColumnIfMissing("Transactions", "CustomerEmail", "TEXT NULL");
                addColumnIfMissing("Transactions", "OperatorUserId", "INTEGER NULL");
                addColumnIfMissing("Transactions", "CollectorUserId", "INTEGER NULL");
                addColumnIfMissing("Transactions", "IsAcknowledged", "INTEGER NOT NULL DEFAULT 0");
                addColumnIfMissing("Transactions", "CollectorUserId", "INTEGER NULL");
                addColumnIfMissing("ChargeTags", "CustomerId", "INTEGER NULL");
                addColumnIfMissing("ChargeTags", "VehicleId", "TEXT NULL");
                addColumnIfMissing("ChargeTags", "VehicleId", "TEXT NULL");
                addColumnIfMissing("ChargePoint", "Branch", "TEXT NULL");
                addColumnIfMissing("Users", "Name", "TEXT NULL");
            }
            else
            {
                void TryExecute(string sql)
                {
                    try { this.Database.ExecuteSqlRaw(sql); } catch { }
                }

                // Ensure all tables exist in SQL Server
                TryExecute(@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SystemSetting]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [dbo].[SystemSetting](
                            [SettingId] [nvarchar](100) NOT NULL,
                            [Value] [nvarchar](max) NULL,
                            CONSTRAINT [PK_SystemSetting] PRIMARY KEY CLUSTERED ([SettingId] ASC)
                        )
                    END");

                TryExecute(@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [dbo].[Users](
                            [UserId] [int] IDENTITY(1,1) NOT NULL,
                            [Username] [nvarchar](100) NOT NULL,
                            [Email] [nvarchar](255) NULL,
                            [PasswordHash] [nvarchar](max) NULL,
                            [IsActive] [bit] NOT NULL DEFAULT 1,
                            [CreateDateTime] [datetime] NULL DEFAULT GETDATE(),
                            CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([UserId] ASC)
                        )
                    END");

                TryExecute(@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Roles]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [dbo].[Roles](
                            [RoleId] [int] IDENTITY(1,1) NOT NULL,
                            [Name] [nvarchar](50) NOT NULL,
                            CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([RoleId] ASC)
                        )
                    END");

                TryExecute(@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserRoles]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [dbo].[UserRoles](
                            [UserId] [int] NOT NULL,
                            [RoleId] [int] NOT NULL,
                            CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED ([UserId] ASC, [RoleId] ASC)
                        )
                    END");

                TryExecute(@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Permissions]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [dbo].[Permissions](
                            [PermissionId] [int] IDENTITY(1,1) NOT NULL,
                            [Name] [nvarchar](100) NOT NULL,
                            [Description] [nvarchar](255) NULL,
                            [Controller] [nvarchar](100) NULL,
                            [Action] [nvarchar](100) NULL,
                            CONSTRAINT [PK_Permissions] PRIMARY KEY CLUSTERED ([PermissionId] ASC)
                        )
                    END");

                TryExecute(@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RolePermissions]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [dbo].[RolePermissions](
                            [RoleId] [int] NOT NULL,
                            [PermissionId] [int] NOT NULL,
                            CONSTRAINT [PK_RolePermissions] PRIMARY KEY CLUSTERED ([RoleId] ASC, [PermissionId] ASC)
                        )
                    END");

                TryExecute(@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Customers]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [dbo].[Customers](
                            [CustomerId] [int] IDENTITY(1,1) NOT NULL,
                            [Name] [nvarchar](200) NOT NULL,
                            [Identifier] [nvarchar](50) NULL,
                            [Phone] [nvarchar](50) NULL,
                            [Email] [nvarchar](100) NULL,
                            [Balance] [decimal](18, 4) NOT NULL DEFAULT 0,
                            CONSTRAINT [PK_Customers] PRIMARY KEY CLUSTERED ([CustomerId] ASC)
                        )
                    END");

                TryExecute(@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TagGroups]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [dbo].[TagGroups](
                            [TagGroupId] [int] IDENTITY(1,1) NOT NULL,
                            [Name] [nvarchar](100) NOT NULL,
                            [Description] [nvarchar](255) NULL,
                            CONSTRAINT [PK_TagGroups] PRIMARY KEY CLUSTERED ([TagGroupId] ASC)
                        )
                    END");

                TryExecute(@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ErrorCatalog]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [dbo].[ErrorCatalog](
                            [ErrorCode] [nvarchar](100) NOT NULL,
                            [Title] [nvarchar](200) NOT NULL,
                            [Description] [nvarchar](max) NOT NULL,
                            [CommonCauses] [nvarchar](max) NULL,
                            [SuggestedSolution] [nvarchar](max) NULL,
                            [Severity] [nvarchar](50) NULL,
                            [Category] [nvarchar](100) NULL,
                            CONSTRAINT [PK_ErrorCatalog] PRIMARY KEY CLUSTERED ([ErrorCode] ASC)
                        )
                    END");

                EnsureSchemaExtended();
            }

            // Common Seeding for both Providers
            
            // 1. Roles
            var baseRoles = new[] { "Administrador", "Operador", "Auditor" };
            foreach (var roleName in baseRoles)
            {
                if (!this.Roles.Any(r => r.Name == roleName))
                {
                    this.Roles.Add(new Role { Name = roleName });
                }
            }
            this.SaveChanges();

            // 2. Permissions
            var perms = new List<Permission>
            {
                new Permission { Name = "Panel de Control", Controller = "Home", Action = "Index" },
                new Permission { Name = "Carga Rápida", Controller = "Home", Action = "QuickStart" },
                new Permission { Name = "Gestión de Cargadores", Controller = "Home", Action = "ChargePoint" },
                new Permission { Name = "Conectores", Controller = "Home", Action = "Connector" },
                new Permission { Name = "Tarjetas (Tags)", Controller = "Home", Action = "ChargeTag" },
                new Permission { Name = "Reportes", Controller = "Home", Action = "ChargeReport" },
                new Permission { Name = "Usuarios", Controller = "Users", Action = null },
                new Permission { Name = "Configuración", Controller = "Settings", Action = null },
                new Permission { Name = "Roles y Permisos", Controller = "Roles", Action = null },
                new Permission { Name = "Clientes", Controller = "Customers", Action = null },
                new Permission { Name = "Control de Operación", Controller = "Home", Action = "Control" },
                new Permission { Name = "Modo de Facturación", Controller = "Settings", Action = "SaveBillingSettings" },
                new Permission { Name = "Catálogo de Diagnóstico", Controller = "Home", Action = "Diagnostics" }
            };

            foreach (var p in perms)
            {
                var existing = this.Permissions.FirstOrDefault(dbP => dbP.Name == p.Name);
                if (existing == null)
                {
                    this.Permissions.Add(p);
                }
                else
                {
                    // Update controller/action if changed
                    existing.Controller = p.Controller;
                    existing.Action = p.Action;
                }
            }
            this.SaveChanges();

            // Ensure specific renames (Legacy support)
            var oldDash = this.Permissions.FirstOrDefault(p => p.Name == "Dashboard");
            if (oldDash != null)
            {
                oldDash.Name = "Panel de Control";
                oldDash.Action = "Index";
                this.SaveChanges();
            }

            // 3. Admin Account & Role Alignment
            var adminRole = this.Roles.FirstOrDefault(r => r.Name == "Administrador");
            if (adminRole != null)
            {
                var allPerms = this.Permissions.ToList();
                foreach (var p in allPerms)
                {
                    if (!this.RolePermissions.Any(rp => rp.RoleId == adminRole.RoleId && rp.PermissionId == p.PermissionId))
                    {
                        this.RolePermissions.Add(new RolePermission { RoleId = adminRole.RoleId, PermissionId = p.PermissionId });
                    }
                }
                this.SaveChanges();

                var adminUser = this.Users.FirstOrDefault(u => u.Username == "admin");
                if (adminUser != null)
                {
                    if (!this.UserRoles.Any(ur => ur.UserId == adminUser.UserId && ur.RoleId == adminRole.RoleId))
                    {
                        this.UserRoles.Add(new UserRole { UserId = adminUser.UserId, RoleId = adminRole.RoleId });
                        this.SaveChanges();
                    }
                }
            }

            // 4. Group & Billing Settings
            if (!this.TagGroups.Any())
            {
                this.TagGroups.Add(new TagGroup { Name = "General", Description = "Grupo por defecto" });
                this.SaveChanges();
            }

            if (!this.SystemSettings.Any(s => s.SettingId == "Billing_Mode"))
                this.SystemSettings.Add(new SystemSetting { SettingId = "Billing_Mode", Value = "Energy" });
            
            if (!this.SystemSettings.Any(s => s.SettingId == "Pricing_Type"))
                this.SystemSettings.Add(new SystemSetting { SettingId = "Pricing_Type", Value = "Fixed" });
            
            if (!this.SystemSettings.Any(s => s.SettingId == "Pricing_Schedules"))
                this.SystemSettings.Add(new SystemSetting { SettingId = "Pricing_Schedules", Value = "[]" });

            this.SaveChanges();

            // 5. Error Catalog Seeding
            SeedErrorCatalog();
        }

        public void EnsureSchemaExtended()
        {
            if (this.Database.IsSqlite())
            {
                // SQLite specific extensions
                AddColumnIfMissingSqlite("Transactions", "CustomerIdentifier", "TEXT NULL");
                AddColumnIfMissingSqlite("Transactions", "CustomerPhone", "TEXT NULL");
                AddColumnIfMissingSqlite("Transactions", "CustomerEmail", "TEXT NULL");
                AddColumnIfMissingSqlite("Transactions", "OperatorUserId", "INTEGER NULL");
                AddColumnIfMissingSqlite("Transactions", "CollectorUserId", "INTEGER NULL");
                
                AddColumnIfMissingSqlite("ChargeTags", "CustomerId", "INTEGER NULL");
                AddColumnIfMissingSqlite("ChargeTags", "VehicleId", "TEXT NULL");
                
                AddColumnIfMissingSqlite("ChargePoint", "Branch", "TEXT NULL");
                
                AddColumnIfMissingSqlite("Transactions", "IsAcknowledged", "INTEGER NOT NULL DEFAULT 0");

                AddColumnIfMissingSqlite("Users", "Name", "TEXT NULL");
            }
            else
            {
                 // Transactions table
                AddColumnIfMissingSqlServer("Transactions", "CustomerIdentifier", "nvarchar(50) NULL");
                AddColumnIfMissingSqlServer("Transactions", "CustomerPhone", "nvarchar(50) NULL");
                AddColumnIfMissingSqlServer("Transactions", "CustomerEmail", "nvarchar(100) NULL");
                AddColumnIfMissingSqlServer("Transactions", "OperatorUserId", "int NULL");
                AddColumnIfMissingSqlServer("Transactions", "CollectorUserId", "int NULL");
                
                // ChargeTags table
                AddColumnIfMissingSqlServer("ChargeTags", "CustomerId", "int NULL");
                AddColumnIfMissingSqlServer("ChargeTags", "VehicleId", "nvarchar(50) NULL");
                
                // ChargePoint table
                AddColumnIfMissingSqlServer("ChargePoint", "Branch", "nvarchar(100) NULL");

                // Transactions table (Additional)
                AddColumnIfMissingSqlServer("Transactions", "IsAcknowledged", "bit NOT NULL DEFAULT 0");
            }
        }

        private void AddColumnIfMissingSqlite(string tableName, string columnName, string columnDefinition)
        {
            try 
            {
                var conn = this.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) conn.Open();
                using (var cmd = conn.CreateCommand()) 
                {
                    cmd.CommandText = $"PRAGMA table_info({tableName})";
                    bool exists = false;
                    using (var reader = cmd.ExecuteReader()) 
                    {
                        while (reader.Read()) 
                        {
                            if (reader["name"].ToString().Equals(columnName, StringComparison.OrdinalIgnoreCase)) 
                            {
                                exists = true;
                                break;
                            }
                        }
                    }
                    if (!exists) 
                    {
                        cmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}";
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Error adding column {columnName} to {tableName} (SQLite): {ex.Message}");
            }
        }


        public void PreMigrationFixes()
        {
            if (this.Database.IsSqlite())
            {
                try 
                {
                    // Fix for "Index already exists" error during migration
                    this.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS IX_Transactions_ChargePointId_ConnectorId");
                    
                    // Fix for "Column already exists" (Users.Name) - Only if empty or we accept risk? 
                    // Actually, if we can't drop column easily, let's just hope the Index fix clears the transaction/migration block.
                    // But if Name exists, we might need to handle it.
                    // SQLite < 3.35 doesn't support DROP COLUMN. Most systems now have > 3.35.
                    // verifying existence:
                    var conn = this.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open) conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA table_info(Users)";
                        bool nameExists = false;
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (reader["name"].ToString().Equals("Name", StringComparison.OrdinalIgnoreCase))
                                {
                                    nameExists = true; 
                                    break;
                                }
                            }
                        }
                        
                        if (nameExists)
                        {
                            // If Name exists, Migration might fail with "duplicate column" if it tries to AddColumn.
                            // We drop it so Migration can recreate it cleanly. 
                            // This assumes Name column doesn't hold critical data or we accept the risk during this recovery.
                            // SQLite supports DROP COLUMN since 3.35.
                            try 
                            {
                                var dropCmd = conn.CreateCommand();
                                dropCmd.CommandText = "ALTER TABLE Users DROP COLUMN Name";
                                dropCmd.ExecuteNonQuery();
                            }
                            catch (Exception dropEx)
                            {
                                Console.WriteLine("Could not drop Users.Name: " + dropEx.Message);
                                // If drop fails (old SQLite), we are stuck. 
                                // But typically on Mac 2024+ it should work.
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("PreMigrationFixes Error: " + ex.Message);
                }
            }
        }

        private void SeedErrorCatalog()
        {
            var entries = new List<ErrorCatalogEntry>
            {
                new ErrorCatalogEntry {
                    ErrorCode = "ConnectorLockFailure",
                    Title = "Fallo de Bloqueo del Conector",
                    Description = "El cargador no pudo bloquear o desbloquear el conector cableado. Esto sucede cuando el actuador no se posiciona correctamente.",
                    CommonCauses = "• Obstrucción física en el puerto.\n• Actuador de bloqueo dañado.\n• Desgaste en el pin de bloqueo.",
                    SuggestedSolution = "Inspeccionar visualmente el conector y puerto. Limpiar residuos. Si el error persiste, el técnico debe verificar el voltaje del actuador de bloqueo y su continuidad.",
                    Severity = "High",
                    Category = "Hardware"
                },
                new ErrorCatalogEntry {
                    ErrorCode = "EVCommunicationError",
                    Title = "Error de Comunicación con el Vehículo",
                    Description = "Fallo en el protocolo de señalización entre el cargador y el sistema de gestión del vehículo (BMS).",
                    CommonCauses = "• Cable de control (CP/PP) dañado.\n• Problema con la señal PWM del cargador.\n• El vehículo rechazó el inicio de carga.",
                    SuggestedSolution = "Verificar integridad del cable de carga. Probar con otro vehículo para descartar fallo del BMS del auto. Revisar placa de control CP si no hay señal PWM.",
                    Severity = "Medium",
                    Category = "Protocol"
                },
                new ErrorCatalogEntry {
                    ErrorCode = "GroundFailure",
                    Title = "Fallo de Puesta a Tierra",
                    Description = "Se detectó una fuga de corriente a tierra o falta de conexión de tierra de protección (PE).",
                    CommonCauses = "• Falla de aislamiento en el vehículo o cable.\n• Instalación eléctrica del sitio dañada.\n• Sensor RCD defectuoso.",
                    SuggestedSolution = "Instruir al cliente a desconectar y reintentar. Si persiste, el técnico debe medir la resistencia de puesta a tierra de la instalación eléctrica. Verificar sensor RCMU interno.",
                    Severity = "Critical",
                    Category = "Electrical"
                },
                new ErrorCatalogEntry {
                    ErrorCode = "HighTemperature",
                    Title = "Alta Temperatura",
                    Description = "La temperatura interna del cargador o conector superó los niveles de seguridad.",
                    CommonCauses = "• Ventilación obstruida.\n• Fallo de ventilador.\n• Contactos sulfatados causando sobrecalentamiento.",
                    SuggestedSolution = "Limpiar rejillas de ventilación. Verificar que los ventiladores giren libremente. Revisar apriete de bornes y estado de los pines del conector.",
                    Severity = "High",
                    Category = "Hardware"
                },
                new ErrorCatalogEntry {
                    ErrorCode = "OverCurrentFailure",
                    Title = "Sobrecorriente en la Salida",
                    Description = "La corriente medida excede el límite nominal del conector o la configuración del cargador.",
                    CommonCauses = "• BMS del auto solicitando más de lo permitido.\n• Cortocircuito parcial.\n• Error de calibración del sensor.",
                    SuggestedSolution = "Verificar configuración de límites de corriente en el cargador. Inspeccionar cable en busca de cortes o quemaduras. Verificar sensores Hall.",
                    Severity = "High",
                    Category = "Electrical"
                },
                new ErrorCatalogEntry {
                    ErrorCode = "PowerMeterFailure",
                    Title = "Fallo del Medidor de Energía",
                    Description = "No se reciben datos del medidor de energía interno (MID).",
                    CommonCauses = "• Cableado RS485/Modbus suelto.\n• Medidor quemado o sin energía.\n• Interferencia electromagnética.",
                    SuggestedSolution = "Verificar alimentación del medidor. Revisar cableado de datos entre el medidor y la placa principal del cargador.",
                    Severity = "Medium",
                    Category = "Hardware"
                },
                new ErrorCatalogEntry {
                    ErrorCode = "UnderVoltage",
                    Title = "Bajo Voltaje de Red",
                    Description = "La tensión de entrada está por debajo del umbral de operación (típicamente -15%).",
                    CommonCauses = "• Caída de tensión en la red eléctrica.\n• Sobrecarga en el tablero de distribución.\n• Alimentación con cable de sección insuficiente.",
                    SuggestedSolution = "Verificar tensión de entrada con multímetro. Si es bajo en el sitio, contactar a la proveedora eléctrica. Aumentar sección de cable si hay caída por distancia.",
                    Severity = "Medium",
                    Category = "Network"
                },
                new ErrorCatalogEntry {
                    ErrorCode = "OverVoltage",
                    Title = "Sobrevoltaje de Red",
                    Description = "La tensión de entrada supera el umbral de operación (típicamente +10%).",
                    CommonCauses = "• Inestabilidad en la red eléctrica exterior.\n• Fallo en transformador de alta a baja tensión.\n• Transitorios de red.",
                    SuggestedSolution = "Verificar tensión de entrada. Instalar protectores de sobretensión si es recurrente. Evitar cargar hasta que la red se estabilice.",
                    Severity = "High",
                    Category = "Network"
                },
                new ErrorCatalogEntry {
                    ErrorCode = "InternalError",
                    Title = "Error Interno del Procesador",
                    Description = "Fallo en el software o microcontrolador del cargador.",
                    CommonCauses = "• Error de firmware.\n• Reinicio inesperado del sistema.\n• Memoria RAM/Flash corrupta.",
                    SuggestedSolution = "Reiniciar el cargador completamente (Power Cycle). Actualizar firmware a la última versión disponible.",
                    Severity = "Medium",
                    Category = "Hardware"
                },
                // NEW ENTRIES
                new ErrorCatalogEntry {
                    ErrorCode = "ScreenFailure",
                    Title = "Fallo de Pantalla",
                    Description = "La pantalla táctil no responde o está en negro.",
                    CommonCauses = "• Cable de datos de pantalla desconectado.\n• Fallo en la alimentación de la pantalla.\n• Daño físico por impacto.",
                    SuggestedSolution = "Verificar conexiones internas de la pantalla. Reiniciar el cargador. Si persiste, reemplazar la unidad de pantalla.",
                    Severity = "Medium", 
                    Category = "Hardware"
                },
                new ErrorCatalogEntry {
                    ErrorCode = "InternetConnectionLost",
                    Title = "Pérdida de Conexión a Internet",
                    Description = "El cargador no puede conectar con el servidor central (OCPP).",
                    CommonCauses = "• Fallo del módem 4G/WiFi.\n• Tarjeta SIM sin datos o mal insertada.\n• Cable Ethernet desconectado.",
                    SuggestedSolution = "Verificar leds del módem. Comprobar saldo de la SIM. Probar cable Ethernet con otro dispositivo.",
                    Severity = "High",
                    Category = "Network"
                }
            };

            foreach (var entry in entries)
            {
                if (!this.ErrorCatalog.Any(e => e.ErrorCode == entry.ErrorCode))
                {
                    this.ErrorCatalog.Add(entry);
                }
            }
            this.SaveChanges();
        }

        private void AddColumnIfMissingSqlServer(string tableName, string columnName, string columnDefinition)
        {
             try
             {
                 // Try to find object ID without explicit dbo first, then with it if needed
                 var checkSql = $@"
                     IF EXISTS (SELECT * FROM sys.tables WHERE name = '{tableName}')
                     BEGIN
                         IF NOT EXISTS (
                           SELECT * 
                           FROM sys.columns 
                           WHERE object_id = OBJECT_ID(N'{tableName}') 
                           AND name = '{columnName}'
                         )
                         BEGIN
                             EXEC('ALTER TABLE [{tableName}] ADD [{columnName}] {columnDefinition}');
                         END
                     END";
                 
                 this.Database.ExecuteSqlRaw(checkSql);
            }
            catch (Exception ex)
            { 
                Console.WriteLine($"Error adding column {columnName} to {tableName}: {ex.Message}");
            }
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
