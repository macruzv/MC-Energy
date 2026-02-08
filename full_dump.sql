PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" ("MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY, "ProductVersion" TEXT NOT NULL);
INSERT INTO __EFMigrationsHistory VALUES('20240405204318_TransactionsIndex','8.0.0');
INSERT INTO __EFMigrationsHistory VALUES('20260124202842_AddBalanceToChargeTags','8.0.17');
CREATE TABLE IF NOT EXISTS "Customers" (
    "CustomerId" INTEGER NOT NULL CONSTRAINT "PK_Customers" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Identifier" TEXT NULL,
    "Phone" TEXT NULL,
    "Email" TEXT NULL
);
CREATE TABLE IF NOT EXISTS "ErrorCatalog" (
    "ErrorCode" TEXT NOT NULL CONSTRAINT "PK_ErrorCatalog" PRIMARY KEY,
    "Title" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "CommonCauses" TEXT NULL,
    "SuggestedSolution" TEXT NULL,
    "Severity" TEXT NULL,
    "Category" TEXT NULL
);
CREATE TABLE IF NOT EXISTS "Permissions" (
    "PermissionId" INTEGER NOT NULL CONSTRAINT "PK_Permissions" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL,
    "Controller" TEXT NULL,
    "Action" TEXT NULL
);
CREATE TABLE IF NOT EXISTS "Roles" (
    "RoleId" INTEGER NOT NULL CONSTRAINT "PK_Roles" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS "SystemSetting" (
    "SettingId" TEXT NOT NULL CONSTRAINT "PK_SystemSetting" PRIMARY KEY,
    "Value" TEXT NULL
);
CREATE TABLE IF NOT EXISTS "TagGroups" (
    "TagGroupId" INTEGER NOT NULL CONSTRAINT "PK_TagGroups" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL
);
CREATE TABLE IF NOT EXISTS "Users" (
    "UserId" INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,
    "Username" TEXT NOT NULL,
    "Name" TEXT NULL,
    "Email" TEXT NULL,
    "PasswordHash" TEXT NULL,
    "IsActive" INTEGER NOT NULL,
    "CreateDateTime" TEXT NULL
);
CREATE TABLE IF NOT EXISTS "RolePermissions" (
    "RoleId" INTEGER NOT NULL,
    "PermissionId" INTEGER NOT NULL,
    CONSTRAINT "PK_RolePermissions" PRIMARY KEY ("RoleId", "PermissionId"),
    CONSTRAINT "FK_RolePermissions_Permissions_PermissionId" FOREIGN KEY ("PermissionId") REFERENCES "Permissions" ("PermissionId") ON DELETE CASCADE,
    CONSTRAINT "FK_RolePermissions_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("RoleId") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "UserChargePoints" (
    "UserId" INTEGER NOT NULL,
    "ChargePointId" TEXT NOT NULL,
    CONSTRAINT "PK_UserChargePoints" PRIMARY KEY ("UserId", "ChargePointId"),
    CONSTRAINT "FK_UserChargePoints_ChargePoint_ChargePointId" FOREIGN KEY ("ChargePointId") REFERENCES "ChargePoint" ("ChargePointId") ON DELETE CASCADE,
    CONSTRAINT "FK_UserChargePoints_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "UserRoles" (
    "UserId" INTEGER NOT NULL,
    "RoleId" INTEGER NOT NULL,
    CONSTRAINT "PK_UserRoles" PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_UserRoles_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("RoleId") ON DELETE CASCADE,
    CONSTRAINT "FK_UserRoles_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "Transactions" (
    "TransactionId" INTEGER NOT NULL CONSTRAINT "PK_Transactions" PRIMARY KEY AUTOINCREMENT,
    "ChargePointId" TEXT NOT NULL,
    "CollectorUserId" INTEGER NULL,
    "ConnectorId" INTEGER NOT NULL,
    "CustomerEmail" TEXT NULL,
    "CustomerIdentifier" TEXT NULL,
    "CustomerPhone" TEXT NULL,
    "MeterStart" REAL NOT NULL,
    "MeterStop" REAL NULL,
    "OperatorUserId" INTEGER NULL,
    "StartResult" TEXT NULL,
    "StartTagId" TEXT NULL,
    "StartTime" TEXT NOT NULL,
    "StopReason" TEXT NULL,
    "StopTagId" TEXT NULL,
    "StopTime" TEXT NULL,
    "Uid" TEXT NULL,
    CONSTRAINT "FK_Transactions_ChargePoint" FOREIGN KEY ("ChargePointId") REFERENCES "ChargePoint" ("ChargePointId")
);
CREATE TABLE IF NOT EXISTS "MessageLog" (
    "LogId" INTEGER NOT NULL CONSTRAINT "PK_MessageLog" PRIMARY KEY AUTOINCREMENT,
    "ChargePointId" TEXT NOT NULL,
    "ConnectorId" INTEGER NULL,
    "ErrorCode" TEXT NULL,
    "LogTime" TEXT NOT NULL,
    "Message" TEXT NOT NULL,
    "Result" TEXT NULL
);
CREATE TABLE IF NOT EXISTS "ConnectorStatus" (
    "ChargePointId" TEXT NOT NULL,
    "ConnectorId" INTEGER NOT NULL,
    "ConnectorName" TEXT NULL,
    "LastMeter" REAL NULL,
    "LastMeterTime" TEXT NULL,
    "LastStatus" TEXT NULL,
    "LastStatusTime" TEXT NULL,
    CONSTRAINT "PK_ConnectorStatus" PRIMARY KEY ("ChargePointId", "ConnectorId"),
    CONSTRAINT "FK_ConnectorStatus_ChargePoint_ChargePointId" FOREIGN KEY ("ChargePointId") REFERENCES "ChargePoint" ("ChargePointId") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ChargeTags" (
    "TagId" TEXT NOT NULL CONSTRAINT "PK_ChargeKeys" PRIMARY KEY,
    "Balance" TEXT NOT NULL,
    "Blocked" INTEGER NULL,
    "CustomerId" INTEGER NULL,
    "ExpiryDate" TEXT NULL,
    "InfiniteBalance" INTEGER NOT NULL,
    "ParentTagId" TEXT NULL,
    "TagName" TEXT NULL,
    CONSTRAINT "FK_ChargeTags_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("CustomerId") ON DELETE SET NULL
);
CREATE TABLE IF NOT EXISTS "ChargePoint" (
    "ChargePointId" TEXT NOT NULL CONSTRAINT "PK_ChargePoint" PRIMARY KEY,
    "ClientCertThumb" TEXT NULL,
    "Comment" TEXT NULL,
    "CreateDateTime" TEXT NULL,
    "Name" TEXT NULL,
    "Password" TEXT NULL,
    "Username" TEXT NULL
);
INSERT INTO ChargePoint VALUES('CP001',NULL,'OCPP Emulator Test',NULL,'EnergyCoreMC-Test',NULL,NULL);
INSERT INTO sqlite_sequence VALUES('Transactions',0);
INSERT INTO sqlite_sequence VALUES('MessageLog',0);
CREATE VIEW "ConnectorStatusView"
AS
SELECT cs.ChargePointId, cs.ConnectorId, cs.ConnectorName, cs.LastStatus, cs.LastStatusTime, cs.LastMeter, cs.LastMeterTime, t.TransactionId, t.StartTagId, t.StartTime, t.MeterStart, t.StartResult, t.StopTagId, t.StopTime, t.MeterStop, t.StopReason
FROM ConnectorStatus AS cs LEFT OUTER JOIN
     Transactions AS t ON t.ChargePointId = cs.ChargePointId AND t.ConnectorId = cs.ConnectorId
WHERE  (t.TransactionId IS NULL) OR
                  (t.TransactionId IN
                      (SELECT MAX(TransactionId) AS Expr1
                       FROM     Transactions
                       GROUP BY ChargePointId, ConnectorId));
CREATE INDEX "IX_RolePermissions_PermissionId" ON "RolePermissions" ("PermissionId");
CREATE INDEX "IX_UserChargePoints_ChargePointId" ON "UserChargePoints" ("ChargePointId");
CREATE INDEX "IX_UserRoles_RoleId" ON "UserRoles" ("RoleId");
CREATE INDEX "IX_Transactions_ChargePointId_ConnectorId" ON "Transactions" ("ChargePointId", "ConnectorId");
CREATE INDEX "IX_MessageLog_ChargePointId" ON "MessageLog" ("LogTime");
CREATE INDEX "IX_ChargeTags_CustomerId" ON "ChargeTags" ("CustomerId");
CREATE UNIQUE INDEX "ChargePoint_Identifier" ON "ChargePoint" ("ChargePointId");
COMMIT;
