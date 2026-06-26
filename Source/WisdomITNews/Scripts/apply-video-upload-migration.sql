-- Chạy script này nếu migration chưa được apply tự động
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Videos') AND name = 'VideoUrl')
    ALTER TABLE Videos ADD VideoUrl nvarchar(max) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Videos') AND name = 'VideoType')
    ALTER TABLE Videos ADD VideoType nvarchar(max) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Videos') AND name = 'FileSize')
    ALTER TABLE Videos ADD FileSize bigint NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Videos') AND name = 'CreatedByUserId')
    ALTER TABLE Videos ADD CreatedByUserId int NULL;

UPDATE Videos SET VideoType = 'youtube' WHERE VideoType IS NULL;

IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20260626050000_AddVideoUploadFields')
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20260626050000_AddVideoUploadFields', '8.0.0');
