-- ============================================================================
-- SQPortal — migration 005: soft delete, concurrency, audit trail,
--                            unique case numbers, schema version tracking
-- ============================================================================
-- Run this ONCE against an existing SQPortal database (SSMS) before starting
-- the app from this branch. Not needed on a database created with the current
-- Scripts/init.sql, which already includes everything below.
--
-- Adds:
--   - Cases.IsDeleted   BIT        soft-delete flag (deleted cases are hidden,
--                                  never removed; recover with UPDATE ... SET
--                                  IsDeleted = 0)
--   - Cases.RowVersion  ROWVERSION optimistic-concurrency token
--   - unique filtered index on Cases.CaseNumber (numbers are never reused)
--   - dbo.Audits        the audit trail (who did what, when)
--   - dbo.SchemaVersions  applied-script tracking, checked at app startup
--
-- Re-runnable: YES — every step is guarded.
-- ============================================================================

USE [SQPortal];
GO

-- Soft-delete flag
IF COL_LENGTH('dbo.Cases', 'IsDeleted') IS NULL
BEGIN
    ALTER TABLE dbo.Cases ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Cases_IsDeleted DEFAULT (0);
END
GO

-- Optimistic-concurrency token
IF COL_LENGTH('dbo.Cases', 'RowVersion') IS NULL
BEGIN
    ALTER TABLE dbo.Cases ADD [RowVersion] ROWVERSION NOT NULL;
END
GO

-- Case numbers become unique (soft-deleted rows included, so a deleted case's
-- number is never handed out again). 0 is excluded: legacy rows hold it until
-- the app's startup backfill numbers them.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cases_CaseNumber' AND object_id = OBJECT_ID('dbo.Cases') AND is_unique = 0)
BEGIN
    DROP INDEX IX_Cases_CaseNumber ON dbo.Cases;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cases_CaseNumber' AND object_id = OBJECT_ID('dbo.Cases'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_Cases_CaseNumber
        ON dbo.Cases (CaseNumber)
        WHERE CaseNumber > 0;
END
GO

-- Audit trail  (mirrors AuditEntry.cs)
IF OBJECT_ID('dbo.Audits', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Audits
    (
        Id            BIGINT        NOT NULL IDENTITY(1,1),
        TimestampUtc  DATETIME2     NOT NULL,
        [User]        NVARCHAR(200) NOT NULL,
        [Action]      NVARCHAR(60)  NOT NULL,
        CaseId        NVARCHAR(64)  NULL,
        Details       NVARCHAR(400) NOT NULL,
        CONSTRAINT PK_Audits PRIMARY KEY (Id)
    );

    CREATE INDEX IX_Audits_TimestampUtc ON dbo.Audits (TimestampUtc);
END
GO

-- Applied-script tracking  (mirrors SchemaVersion.cs; checked at app startup)
IF OBJECT_ID('dbo.SchemaVersions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaVersions
    (
        Version       NVARCHAR(20) NOT NULL,
        AppliedAtUtc  DATETIME2    NOT NULL CONSTRAINT DF_SchemaVersions_AppliedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_SchemaVersions PRIMARY KEY (Version)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE Version = '005')
BEGIN
    INSERT INTO dbo.SchemaVersions (Version) VALUES ('005');
END
GO
