START TRANSACTION;

CREATE TABLE `learning_rag_jobs` (
    `Id` binary(16) NOT NULL,
    `TopicId` binary(16) NOT NULL,
    `Revision` binary(16) NOT NULL,
    `Status` varchar(24) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `Attempts` int NOT NULL,
    `ChunkCount` int NOT NULL,
    `ErrorCode` varchar(80) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `NextAttemptAt` datetime(6) NOT NULL,
    `LeaseUntil` datetime(6) NULL,
    `LeaseToken` binary(16) NULL,
    `CompletedAt` datetime(6) NULL,
    CONSTRAINT `PK_learning_rag_jobs` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE INDEX `IX_learning_rag_jobs_Status_NextAttemptAt` ON `learning_rag_jobs` (`Status`, `NextAttemptAt`);

CREATE UNIQUE INDEX `IX_learning_rag_jobs_TopicId_Revision` ON `learning_rag_jobs` (`TopicId`, `Revision`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261017014214_LearningRagOutbox', '8.0.31');

COMMIT;

START TRANSACTION;

CREATE TABLE `learning_rag_drafts` (
    `Id` binary(16) NOT NULL,
    `TopicId` binary(16) NOT NULL,
    `SourceRevision` binary(16) NOT NULL,
    `CreatedBy` binary(16) NOT NULL,
    `Kind` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `Language` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `Content` json NOT NULL,
    `Sources` json NOT NULL,
    `Status` varchar(24) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `Validation` varchar(80) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `RowVersion` binary(16) NOT NULL,
    `ReviewedBy` binary(16) NULL,
    `ReviewedAt` datetime(6) NULL,
    CONSTRAINT `PK_learning_rag_drafts` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE INDEX `IX_learning_rag_drafts_TopicId` ON `learning_rag_drafts` (`TopicId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261017014949_LearningRagDrafts', '8.0.31');

COMMIT;

