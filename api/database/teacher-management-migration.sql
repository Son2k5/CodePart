START TRANSACTION;

ALTER TABLE `users` ADD `Bio` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL;

ALTER TABLE `users` ADD `PhoneNumber` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL;

ALTER TABLE `users` ADD `TeacherId` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL;

CREATE UNIQUE INDEX `ux_users_teacher_id` ON `users` (`TeacherId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261015010722_AddTeacherFieldsAndEnableAction', '8.0.31');

COMMIT;

