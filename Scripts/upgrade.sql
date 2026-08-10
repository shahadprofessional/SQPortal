-- ============================================================================
-- SQPortal — upgrade an existing database                    schema version 006
-- ============================================================================
-- Run ONCE against an EXISTING SQPortal database created from any earlier
-- version of these scripts. Not needed on a database created with the current
-- init.sql. Back up the database first. See Scripts/README.md.
--
-- Every step is guarded, so the script is safe to re-run and skips whatever
-- is already in place. It consolidates the former incremental scripts
-- (002 case numbers, 004 branch managers, 005 soft delete / concurrency /
-- audit / version tracking, 006 per-case audit index).
--
-- The final schema version rows are only recorded when every step succeeded,
-- so the app's startup check reports a partially-upgraded database.
-- ============================================================================

USE [SQPortal];
GO

-- ----------------------------------------------------------------------------
-- 1. Cases.CaseNumber — human-facing incremental case number
-- ----------------------------------------------------------------------------
IF COL_LENGTH('dbo.Cases', 'CaseNumber') IS NULL
BEGIN
    ALTER TABLE dbo.Cases
        ADD CaseNumber INT NOT NULL CONSTRAINT DF_Cases_CaseNumber DEFAULT 0;
END
GO

-- Number anything still at 0, oldest case date first (ties broken by Id, a
-- unix-ms timestamp, so creation order), continuing from the highest number
-- in use. The app's startup backfill does the same for rows added later.
DECLARE @offset INT = (SELECT ISNULL(MAX(CaseNumber), 0) FROM dbo.Cases);

;WITH numbered AS
(
    SELECT CaseNumber,
           ROW_NUMBER() OVER (ORDER BY [Date], Id) AS rn
    FROM dbo.Cases
    WHERE CaseNumber = 0
)
UPDATE numbered
SET CaseNumber = @offset + rn;
GO

-- ----------------------------------------------------------------------------
-- 2. Branch managers  (mirror BranchManager.cs / BranchManagerAssignment.cs)
-- ----------------------------------------------------------------------------
IF OBJECT_ID('dbo.Managers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Managers
    (
        Name      NVARCHAR(40)  NOT NULL,
        FullName  NVARCHAR(200) NOT NULL,
        Email     NVARCHAR(200) NOT NULL,
        CONSTRAINT PK_Managers PRIMARY KEY (Name)
    );
END
GO

IF OBJECT_ID('dbo.ManagerAssignments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ManagerAssignments
    (
        BranchName       NVARCHAR(100) NOT NULL,
        AssignedManager  NVARCHAR(40)  NOT NULL,
        CONSTRAINT PK_ManagerAssignments PRIMARY KEY (BranchName)
    );
END
GO

-- ----------------------------------------------------------------------------
-- 3. Soft delete and optimistic concurrency on Cases
-- ----------------------------------------------------------------------------
IF COL_LENGTH('dbo.Cases', 'IsDeleted') IS NULL
BEGIN
    ALTER TABLE dbo.Cases ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Cases_IsDeleted DEFAULT (0);
END
GO

IF COL_LENGTH('dbo.Cases', 'RowVersion') IS NULL
BEGIN
    ALTER TABLE dbo.Cases ADD [RowVersion] ROWVERSION NOT NULL;
END
GO

-- ----------------------------------------------------------------------------
-- 4. Unique case numbers
--    If duplicates exist (only possible on databases numbered by a very early
--    script), the index is skipped and the message below explains the fix;
--    the version rows in step 7 are then not written, so the app's startup
--    check keeps pointing here until it is resolved.
-- ----------------------------------------------------------------------------
IF EXISTS (SELECT CaseNumber FROM dbo.Cases WHERE CaseNumber > 0
           GROUP BY CaseNumber HAVING COUNT(*) > 1)
BEGIN
    PRINT '!! Duplicate CaseNumber values exist — unique index NOT created.';
    PRINT '!! Fix by renumbering all cases, then re-run this script:';
    PRINT '!!   ;WITH n AS (SELECT CaseNumber, ROW_NUMBER() OVER (ORDER BY [Date], Id) rn FROM dbo.Cases)';
    PRINT '!!   UPDATE n SET CaseNumber = rn;';
END
ELSE
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cases_CaseNumber'
               AND object_id = OBJECT_ID('dbo.Cases') AND is_unique = 0)
    BEGIN
        DROP INDEX IX_Cases_CaseNumber ON dbo.Cases;
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cases_CaseNumber'
                   AND object_id = OBJECT_ID('dbo.Cases'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX IX_Cases_CaseNumber
            ON dbo.Cases (CaseNumber)
            WHERE CaseNumber > 0;
    END
END
GO

-- ----------------------------------------------------------------------------
-- 5. Drop the unused customer-phone index (nothing queries by phone in SQL)
-- ----------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cases_CustomerPhone'
           AND object_id = OBJECT_ID('dbo.Cases'))
BEGIN
    DROP INDEX IX_Cases_CustomerPhone ON dbo.Cases;
END
GO

-- ----------------------------------------------------------------------------
-- 6. Audit trail  (mirrors AuditEntry.cs)
-- ----------------------------------------------------------------------------
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
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Audits_TimestampUtc' AND object_id = OBJECT_ID('dbo.Audits'))
BEGIN
    CREATE INDEX IX_Audits_TimestampUtc ON dbo.Audits (TimestampUtc);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Audits_CaseId' AND object_id = OBJECT_ID('dbo.Audits'))
BEGIN
    CREATE INDEX IX_Audits_CaseId ON dbo.Audits (CaseId);
END
GO

-- ----------------------------------------------------------------------------
-- 7. Schema version tracking  (mirrors SchemaVersion.cs; checked at startup)
--    Version rows are only written when the unique case-number index exists,
--    i.e. when every earlier step actually completed.
-- ----------------------------------------------------------------------------
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

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cases_CaseNumber'
           AND object_id = OBJECT_ID('dbo.Cases') AND is_unique = 1)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE Version = '005')
        INSERT INTO dbo.SchemaVersions (Version) VALUES ('005');
    IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE Version = '006')
        INSERT INTO dbo.SchemaVersions (Version) VALUES ('006');
END
GO

-- ============================================================================
-- Done. Start the app; a critical log line at startup means a step above was
-- skipped — read the messages this script printed.
-- ============================================================================
