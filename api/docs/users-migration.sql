START TRANSACTION;

ALTER TABLE `users` ROW_FORMAT=DYNAMIC;

ALTER TABLE `users` COLLATE=utf8mb4_unicode_ci;

ALTER TABLE `users` MODIFY COLUMN `UpdatedBy` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL;

ALTER TABLE `users` MODIFY COLUMN `TeacherId` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL;

ALTER TABLE `users` MODIFY COLUMN `Status` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL;

ALTER TABLE `users` MODIFY COLUMN `Role` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL;

ALTER TABLE `users` MODIFY COLUMN `PhoneNumber` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL;

ALTER TABLE `users` MODIFY COLUMN `PasswordHash` varchar(256) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL;

ALTER TABLE `users` MODIFY COLUMN `FullName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL;

ALTER TABLE `users` MODIFY COLUMN `Email` varchar(256) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL;

ALTER TABLE `users` MODIFY COLUMN `CreatedBy` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL;

ALTER TABLE `users` MODIFY COLUMN `Bio` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL;

ALTER TABLE `users` ADD `AcademicTitle` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL;

ALTER TABLE `users` ADD `AvatarUrl` varchar(2048) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL;

ALTER TABLE `users` ADD `DateOfBirth` date NULL;

ALTER TABLE `users` ADD `Gender` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL;

ALTER TABLE `users` ADD `Version` int NOT NULL DEFAULT 1;

ALTER TABLE `audit_logs` ADD `NewValue` json NULL;

ALTER TABLE `audit_logs` ADD `OldValue` json NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261024012649_AddUserProfile', '8.0.31');

COMMIT;

