-- MariaDB Backup Script (Migrated from SQLite)
-- Created: 2026-01-26
--
-- PURPOSE:
-- This script imports the data from your SQLite database into MariaDB.
--
-- INSTRUCTIONS:
-- 1. Create the database 'OCPP-MC' in MariaDB if it doesn't exist.
-- 2. Run the application (OCPP.Core.Management) once. This will:
--    - Create all the tables (Schema).
--    - Seed default data (Default Users 'admin'/'operator', Roles, Permissions, etc.).
-- 3. Run this script to import your custom data (ChargePoints, etc.).
--    Command: mysql -u root -p OCPP-MC < backup_sqlite_to_mariadb.sql
--
-- NOTES:
-- - This script uses 'INSERT IGNORE' to prevent errors if the data already exists.

SET FOREIGN_KEY_CHECKS = 0;
SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;

--
-- Importing Data for table: ChargePoint
--
INSERT IGNORE INTO ChargePoint (ChargePointId, ClientCertThumb, Comment, CreateDateTime, Name, Password, Username) 
VALUES ('CP001',NULL,'OCPP Emulator Test',NULL,'EnergyCoreMC-Test',NULL,NULL);

--
-- (Add other tables here if they had data in SQLite)
--

COMMIT;
SET FOREIGN_KEY_CHECKS = 1;

SELECT 'Backup import completed successfully.' AS Status;
