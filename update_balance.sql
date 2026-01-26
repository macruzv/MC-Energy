BEGIN TRANSACTION;

ALTER TABLE "Transactions" ADD "CollectorUserId" INTEGER NULL;

ALTER TABLE "Transactions" ADD "CustomerEmail" TEXT NULL;

ALTER TABLE "Transactions" ADD "CustomerIdentifier" TEXT NULL;

ALTER TABLE "Transactions" ADD "CustomerPhone" TEXT NULL;

ALTER TABLE "Transactions" ADD "OperatorUserId" INTEGER NULL;

ALTER TABLE "ChargeTags" ADD "Balance" TEXT NOT NULL DEFAULT '0.0';

ALTER TABLE "ChargeTags" ADD "CustomerId" INTEGER NULL;

ALTER TABLE "ChargeTags" ADD "InfiniteBalance" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "ChargePoint" ADD "CreateDateTime" TEXT NULL;

CREATE TABLE "Customers" (
    "CustomerId" INTEGER NOT NULL CONSTRAINT "PK_Customers" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Identifier" TEXT NULL,
    "Phone" TEXT NULL,
    "Email" TEXT NULL
);

CREATE TABLE "ErrorCatalog" (
    "ErrorCode" TEXT NOT NULL CONSTRAINT "PK_ErrorCatalog" PRIMARY KEY,
    "Title" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "CommonCauses" TEXT NULL,
    "SuggestedSolution" TEXT NULL,
    "Severity" TEXT NULL,
    "Category" TEXT NULL
);

CREATE TABLE "Permissions" (
    "PermissionId" INTEGER NOT NULL CONSTRAINT "PK_Permissions" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL,
    "Controller" TEXT NULL,
    "Action" TEXT NULL
);

CREATE TABLE "Roles" (
    "RoleId" INTEGER NOT NULL CONSTRAINT "PK_Roles" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL
);

CREATE TABLE "SystemSetting" (
    "SettingId" TEXT NOT NULL CONSTRAINT "PK_SystemSetting" PRIMARY KEY,
    "Value" TEXT NULL
);

CREATE TABLE "TagGroups" (
    "TagGroupId" INTEGER NOT NULL CONSTRAINT "PK_TagGroups" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL
);

CREATE TABLE "Users" (
    "UserId" INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,
    "Username" TEXT NOT NULL,
    "Name" TEXT NULL,
    "Email" TEXT NULL,
    "PasswordHash" TEXT NULL,
    "IsActive" INTEGER NOT NULL,
    "CreateDateTime" TEXT NULL
);

CREATE TABLE "RolePermissions" (
    "RoleId" INTEGER NOT NULL,
    "PermissionId" INTEGER NOT NULL,
    CONSTRAINT "PK_RolePermissions" PRIMARY KEY ("RoleId", "PermissionId"),
    CONSTRAINT "FK_RolePermissions_Permissions_PermissionId" FOREIGN KEY ("PermissionId") REFERENCES "Permissions" ("PermissionId") ON DELETE CASCADE,
    CONSTRAINT "FK_RolePermissions_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("RoleId") ON DELETE CASCADE
);

CREATE TABLE "UserChargePoints" (
    "UserId" INTEGER NOT NULL,
    "ChargePointId" TEXT NOT NULL,
    CONSTRAINT "PK_UserChargePoints" PRIMARY KEY ("UserId", "ChargePointId"),
    CONSTRAINT "FK_UserChargePoints_ChargePoint_ChargePointId" FOREIGN KEY ("ChargePointId") REFERENCES "ChargePoint" ("ChargePointId") ON DELETE CASCADE,
    CONSTRAINT "FK_UserChargePoints_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE CASCADE
);

CREATE TABLE "UserRoles" (
    "UserId" INTEGER NOT NULL,
    "RoleId" INTEGER NOT NULL,
    CONSTRAINT "PK_UserRoles" PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_UserRoles_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("RoleId") ON DELETE CASCADE,
    CONSTRAINT "FK_UserRoles_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE CASCADE
);

CREATE INDEX "IX_ChargeTags_CustomerId" ON "ChargeTags" ("CustomerId");

CREATE INDEX "IX_RolePermissions_PermissionId" ON "RolePermissions" ("PermissionId");

CREATE INDEX "IX_UserChargePoints_ChargePointId" ON "UserChargePoints" ("ChargePointId");

CREATE INDEX "IX_UserRoles_RoleId" ON "UserRoles" ("RoleId");

CREATE TABLE "ef_temp_Transactions" (
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

INSERT INTO "ef_temp_Transactions" ("TransactionId", "ChargePointId", "CollectorUserId", "ConnectorId", "CustomerEmail", "CustomerIdentifier", "CustomerPhone", "MeterStart", "MeterStop", "OperatorUserId", "StartResult", "StartTagId", "StartTime", "StopReason", "StopTagId", "StopTime", "Uid")
SELECT "TransactionId", "ChargePointId", "CollectorUserId", "ConnectorId", "CustomerEmail", "CustomerIdentifier", "CustomerPhone", "MeterStart", "MeterStop", "OperatorUserId", "StartResult", "StartTagId", "StartTime", "StopReason", "StopTagId", "StopTime", "Uid"
FROM "Transactions";

CREATE TABLE "ef_temp_MessageLog" (
    "LogId" INTEGER NOT NULL CONSTRAINT "PK_MessageLog" PRIMARY KEY AUTOINCREMENT,
    "ChargePointId" TEXT NOT NULL,
    "ConnectorId" INTEGER NULL,
    "ErrorCode" TEXT NULL,
    "LogTime" TEXT NOT NULL,
    "Message" TEXT NOT NULL,
    "Result" TEXT NULL
);

INSERT INTO "ef_temp_MessageLog" ("LogId", "ChargePointId", "ConnectorId", "ErrorCode", "LogTime", "Message", "Result")
SELECT "LogId", "ChargePointId", "ConnectorId", "ErrorCode", "LogTime", "Message", "Result"
FROM "MessageLog";

CREATE TABLE "ef_temp_ConnectorStatus" (
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

INSERT INTO "ef_temp_ConnectorStatus" ("ChargePointId", "ConnectorId", "ConnectorName", "LastMeter", "LastMeterTime", "LastStatus", "LastStatusTime")
SELECT "ChargePointId", "ConnectorId", "ConnectorName", "LastMeter", "LastMeterTime", "LastStatus", "LastStatusTime"
FROM "ConnectorStatus";

CREATE TABLE "ef_temp_ChargeTags" (
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

INSERT INTO "ef_temp_ChargeTags" ("TagId", "Balance", "Blocked", "CustomerId", "ExpiryDate", "InfiniteBalance", "ParentTagId", "TagName")
SELECT "TagId", "Balance", "Blocked", "CustomerId", "ExpiryDate", "InfiniteBalance", "ParentTagId", "TagName"
FROM "ChargeTags";

CREATE TABLE "ef_temp_ChargePoint" (
    "ChargePointId" TEXT NOT NULL CONSTRAINT "PK_ChargePoint" PRIMARY KEY,
    "ClientCertThumb" TEXT NULL,
    "Comment" TEXT NULL,
    "CreateDateTime" TEXT NULL,
    "Name" TEXT NULL,
    "Password" TEXT NULL,
    "Username" TEXT NULL
);

INSERT INTO "ef_temp_ChargePoint" ("ChargePointId", "ClientCertThumb", "Comment", "CreateDateTime", "Name", "Password", "Username")
SELECT "ChargePointId", "ClientCertThumb", "Comment", "CreateDateTime", "Name", "Password", "Username"
FROM "ChargePoint";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;

DROP TABLE "Transactions";

ALTER TABLE "ef_temp_Transactions" RENAME TO "Transactions";

DROP TABLE "MessageLog";

ALTER TABLE "ef_temp_MessageLog" RENAME TO "MessageLog";

DROP TABLE "ConnectorStatus";

ALTER TABLE "ef_temp_ConnectorStatus" RENAME TO "ConnectorStatus";

DROP TABLE "ChargeTags";

ALTER TABLE "ef_temp_ChargeTags" RENAME TO "ChargeTags";

DROP TABLE "ChargePoint";

ALTER TABLE "ef_temp_ChargePoint" RENAME TO "ChargePoint";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;

CREATE INDEX "IX_Transactions_ChargePointId_ConnectorId" ON "Transactions" ("ChargePointId", "ConnectorId");

CREATE INDEX "IX_MessageLog_ChargePointId" ON "MessageLog" ("LogTime");

CREATE INDEX "IX_ChargeTags_CustomerId" ON "ChargeTags" ("CustomerId");

CREATE UNIQUE INDEX "ChargePoint_Identifier" ON "ChargePoint" ("ChargePointId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260124202842_AddBalanceToChargeTags', '8.0.17');

COMMIT;

