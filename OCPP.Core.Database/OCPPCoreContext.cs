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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChargePoint>(entity =>
            {
                entity.ToTable("ChargePoint");

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
                addColumnIfMissing("ChargeTags", "CustomerId", "INTEGER NULL");

                // 1. Asegurar Roles base
                var baseRoles = new[] { "Administrador", "Operador", "Auditor" };
                foreach (var roleName in baseRoles)
                {
                    if (!this.Roles.Any(r => r.Name == roleName))
                    {
                        this.Roles.Add(new Role { Name = roleName });
                    }
                }
                this.SaveChanges();

                // 2. Asegurar Permisos base
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
                    new Permission { Name = "Control de Operación", Controller = "Home", Action = "Control" }
                };

                foreach (var p in perms)
                {
                    if (!this.Permissions.Any(dbP => dbP.Name == p.Name))
                    {
                        this.Permissions.Add(p);
                    }
                }
                this.SaveChanges();

                // 3. Vincular todos los permisos al rol de Administrador
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

                    // Ensure specific rename if Dashboard existed (SQLite/Manual)
                    var oldDash = this.Permissions.FirstOrDefault(p => p.Name == "Dashboard");
                    if (oldDash != null)
                    {
                        oldDash.Name = "Panel de Control";
                        this.SaveChanges();
                    }

                    // 3.b. Asegurar que el usuario 'admin' tenga el rol de Administrador
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

                // 4. Asegurar que exista al menos un grupo por defecto
                if (!this.TagGroups.Any())
                {
                    this.TagGroups.Add(new TagGroup { Name = "General", Description = "Grupo por defecto" });
                    this.SaveChanges();
                }
                return;
            }

            void TryExecute(string sql)
            {
                try { this.Database.ExecuteSqlRaw(sql); } catch { }
            }

            // SQL Server Optimization: Check if SystemSetting exists first
            bool tablesExist = false;
            try {
                var conn = this.Database.GetDbConnection();
                bool shouldClose = conn.State != System.Data.ConnectionState.Open;
                if (shouldClose) conn.Open();
                try {
                    using (var cmd = conn.CreateCommand()) {
                        cmd.CommandText = "SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SystemSetting]') AND type in (N'U')";
                        var result = cmd.ExecuteScalar();
                        tablesExist = (result != null && result != DBNull.Value);
                    }
                } finally {
                    if (shouldClose && conn.State == System.Data.ConnectionState.Open) conn.Close();
                }
            } catch { }

            if (!tablesExist)
            {
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
                        INSERT INTO [dbo].[Roles] ([Name]) VALUES ('Administrador'), ('Operador'), ('Auditor')
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
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserChargePoints]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [dbo].[UserChargePoints](
                            [UserId] [int] NOT NULL,
                            [ChargePointId] [nvarchar](100) NOT NULL,
                            CONSTRAINT [PK_UserChargePoints] PRIMARY KEY CLUSTERED ([UserId] ASC, [ChargePointId] ASC)
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
                        INSERT INTO [dbo].[Permissions] ([Name], [Controller], [Action]) VALUES 
                        ('Panel de Control', 'Home', 'Index'), ('Carga Rápida', 'Home', 'QuickStart'),
                        ('Gestión de Cargadores', 'Home', 'ChargePoint'), ('Conectores', 'Home', 'Connector'),
                        ('Tarjetas (Tags)', 'Home', 'ChargeTag'), ('Reportes', 'Home', 'ChargeReport'),
                        ('Usuarios', 'Users', NULL), ('Configuración', 'Settings', NULL),
                        ('Roles y Permisos', 'Roles', NULL), ('Control de Operación', 'Home', 'Control')
                    END
                    ELSE
                    BEGIN
                        UPDATE [dbo].[Permissions] SET [Name] = 'Panel de Control', [Action] = 'Index' WHERE [Name] = 'Dashboard'
                        UPDATE [dbo].[Permissions] SET [Name] = 'Gestión de Cargadores', [Action] = 'ChargePoint' WHERE [Name] = 'Cargadores'
                    END");

                TryExecute(@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RolePermissions]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [dbo].[RolePermissions](
                            [RoleId] [int] NOT NULL,
                            [PermissionId] [int] NOT NULL,
                            CONSTRAINT [PK_RolePermissions] PRIMARY KEY CLUSTERED ([RoleId] ASC, [PermissionId] ASC)
                        )
                        DECLARE @AdminRoleId int = (SELECT RoleId FROM [dbo].[Roles] WHERE [Name] = 'Administrador')
                        IF @AdminRoleId IS NOT NULL
                        BEGIN
                            INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId])
                            SELECT @AdminRoleId, [PermissionId] FROM [dbo].[Permissions]
                        END
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
                        INSERT INTO [dbo].[TagGroups] ([Name], [Description]) VALUES ('General', 'Grupo por defecto')
                    END");
            }


            TryExecute(@"
                IF NOT EXISTS (SELECT * FROM [dbo].[Permissions] WHERE [Name] = 'Clientes')
                BEGIN
                    INSERT INTO [dbo].[Permissions] ([Name], [Controller], [Action]) VALUES ('Clientes', 'Customers', NULL)
                    DECLARE @ClientId INT = (SELECT PermissionId FROM [dbo].[Permissions] WHERE [Name] = 'Clientes')
                    INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId])
                    SELECT RoleId, @ClientId FROM [dbo].[Roles] WHERE RoleId IN (1, 2)
                END
                ELSE
                BEGIN
                    UPDATE [dbo].[Permissions] SET [Action] = NULL WHERE [Name] = 'Clientes' AND [Controller] = 'Customers'
                END");
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
