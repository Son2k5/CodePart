START TRANSACTION;

ALTER TABLE `refresh_tokens` ADD `LastActiveAt` datetime(6) NOT NULL DEFAULT '1970-01-01 00:00:00';

ALTER TABLE `refresh_tokens` ADD `RevokedReason` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL;

ALTER TABLE `refresh_tokens` ADD `SessionId` binary(16) NOT NULL DEFAULT 0x00000000000000000000000000000000;

UPDATE refresh_tokens
SET SessionId = FamilyId, LastActiveAt = CreatedAt,
    RevokedReason = CASE WHEN RevokedAt IS NULL THEN 'SESSION_MIGRATION' ELSE RevokedReason END,
    RevokedAt = COALESCE(RevokedAt, UTC_TIMESTAMP(6));

CREATE INDEX `IX_refresh_tokens_SessionId_RevokedAt` ON `refresh_tokens` (`SessionId`, `RevokedAt`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261015070254_AddSessionIdleTimeout', '8.0.31');

COMMIT;

