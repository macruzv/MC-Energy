-- MariaDB Full Dump generated from SQLite
-- Date: 2026-01-26T22:34:06.003501
SET FOREIGN_KEY_CHECKS = 0;
SET SQL_MODE = 'NO_AUTO_VALUE_ON_ZERO';
START TRANSACTION;

-- Data for table: ChargePoint
INSERT IGNORE INTO `ChargePoint` (`ChargePointId`, `ClientCertThumb`, `Comment`, `CreateDateTime`, `Name`, `Password`, `Username`) VALUES ('CP001', NULL, 'OCPP Emulator Test', NULL, 'EnergyCoreMC-Test', NULL, NULL);

COMMIT;
SET FOREIGN_KEY_CHECKS = 1;
