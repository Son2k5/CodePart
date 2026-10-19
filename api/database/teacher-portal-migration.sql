START TRANSACTION;

ALTER TABLE `submissions` ADD `GradedAt` datetime(6) NULL;

ALTER TABLE `submissions` ADD `GradedBy` binary(16) NULL;

ALTER TABLE `submissions` ADD `TeacherFeedback` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL;

ALTER TABLE `submissions` ADD `TeacherScore` decimal(4,2) NULL;

ALTER TABLE `exercises` ADD `CourseId` binary(16) NULL;

CREATE INDEX `IX_exercises_CourseId` ON `exercises` (`CourseId`);

ALTER TABLE `exercises` ADD CONSTRAINT `FK_exercises_courses_CourseId` FOREIGN KEY (`CourseId`) REFERENCES `courses` (`Id`) ON DELETE RESTRICT;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261015013341_TeacherPortalAssignmentsAndGrading', '8.0.31');

COMMIT;

