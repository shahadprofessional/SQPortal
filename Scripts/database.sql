-- ============================================================================
-- SQPortal — database script                                 schema version 006
-- ============================================================================
-- The application never creates or alters tables. This file is the single
-- source of truth for the schema and the only way schema reaches a database.
--
-- HOW TO RUN (SQL Server Management Studio)
--   New empty database ....... run PART 1, then PART 3.
--   Existing database ........ run PART 2, then PART 3.
--   Not sure ................. run PART 2, then PART 3. Part 2 is guarded and
--                              skips whatever already exists, so it is safe on
--                              any database created from an earlier version of
--                              this script. Back up the database first.
--
-- Each part is separated by a clearly marked banner below. Highlight the
-- required part and execute it, or run the whole file on a NEW database
-- (Part 2 is written to be harmless immediately after Part 1).
--
-- AFTER RUNNING
--   Start the app. It verifies the version recorded in dbo.SchemaVersions and
--   logs a critical line naming this file if the database is behind. In
--   Development it also seeds sample branches and staff; UAT and production
--   start empty, so enter the real rosters in Settings.
--
-- KEEPING THIS FILE IN SYNC WITH THE CODE
--   Any table or index change must be made here (in BOTH Part 1 and Part 2),
--   in the matching entity class under Models/Entities/, and — for indexes,
--   filters and converters — in Data/SQPortalDbContext.cs (OnModelCreating).
--   Bump the version in Part 3 and in Program.cs (requiredSchemaVersion).
-- ============================================================================

USE [SQPortal];
GO


-- ############################################################################
-- ############################################################################
-- ##                                                                        ##
-- ##   PART 1 — FRESH INSTALL                                               ##
-- ##   Run against a NEW EMPTY database only. Skip for an existing one.     ##
-- ##                                                                        ##
-- ############################################################################
-- ############################################################################

-- ----------------------------------------------------------------------------
-- 1.0  Database (optional). Skip when "SQPortal" already exists.
-- ----------------------------------------------------------------------------
-- CREATE DATABASE SQPortal;
-- GO

-- ----------------------------------------------------------------------------
-- 1.1  Lookup tables: people and branches
-- ----------------------------------------------------------------------------

-- SQ staff  (mirrors BusinessPartner.cs)
IF OBJECT_ID('dbo.Partners', 'U') IS NULL
CREATE TABLE dbo.Partners
(
    Name      NVARCHAR(40)  NOT NULL,
    FullName  NVARCHAR(200) NOT NULL,
    Email     NVARCHAR(200) NOT NULL,
    CONSTRAINT PK_Partners PRIMARY KEY (Name)
);
GO

-- Branches  (mirrors Branch.cs)
IF OBJECT_ID('dbo.Branches', 'U') IS NULL
CREATE TABLE dbo.Branches
(
    Name NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_Branches PRIMARY KEY (Name)
);
GO

-- Branch managers  (mirrors BranchManager.cs)
IF OBJECT_ID('dbo.Managers', 'U') IS NULL
CREATE TABLE dbo.Managers
(
    Name      NVARCHAR(40)  NOT NULL,
    FullName  NVARCHAR(200) NOT NULL,
    Email     NVARCHAR(200) NOT NULL,
    CONSTRAINT PK_Managers PRIMARY KEY (Name)
);
GO

-- Which SQ staff member handles each branch  (mirrors BranchPartnerAssignment.cs)
IF OBJECT_ID('dbo.BranchAssignments', 'U') IS NULL
CREATE TABLE dbo.BranchAssignments
(
    BranchName       NVARCHAR(100) NOT NULL,
    AssignedPartner  NVARCHAR(40)  NOT NULL,
    CONSTRAINT PK_BranchAssignments PRIMARY KEY (BranchName)
);
GO

-- Which manager runs each branch  (mirrors BranchManagerAssignment.cs)
IF OBJECT_ID('dbo.ManagerAssignments', 'U') IS NULL
CREATE TABLE dbo.ManagerAssignments
(
    BranchName       NVARCHAR(100) NOT NULL,
    AssignedManager  NVARCHAR(40)  NOT NULL,
    CONSTRAINT PK_ManagerAssignments PRIMARY KEY (BranchName)
);
GO

-- ----------------------------------------------------------------------------
-- 1.2  Cases  (mirrors FeedbackCase.cs)
--      RootCauses is JSON text via an EF value converter, hence NVARCHAR(MAX).
--      FollowUpStatus and CaseValidation are enums stored as int values.
--      IsDeleted is the soft-delete flag; RowVersion the concurrency token.
-- ----------------------------------------------------------------------------
IF OBJECT_ID('dbo.Cases', 'U') IS NULL
CREATE TABLE dbo.Cases
(
    Id                NVARCHAR(64)  NOT NULL,
    CaseNumber        INT           NOT NULL CONSTRAINT DF_Cases_CaseNumber DEFAULT 0,
    [Date]            DATE          NOT NULL,
    CustomerName      NVARCHAR(200) NOT NULL,
    CustomerPhone     NVARCHAR(40)  NOT NULL,
    TicketNumber      NVARCHAR(60)  NULL,
    Branch            NVARCHAR(100) NOT NULL,
    BusinessPartner   NVARCHAR(40)  NOT NULL,
    BranchRating      INT           NOT NULL,
    BranchComment     NVARCHAR(MAX) NULL,
    StaffName         NVARCHAR(200) NULL,
    StaffRating       INT           NOT NULL,
    StaffComment      NVARCHAR(MAX) NULL,
    DueDate           DATE          NOT NULL,
    FollowUpStatus    INT           NOT NULL,
    FollowUpDate      DATE          NULL,
    FollowUpNotes     NVARCHAR(MAX) NULL,
    CaseValidation    INT           NOT NULL,
    RootCauses        NVARCHAR(MAX) NOT NULL,
    ValidityStatus    NVARCHAR(MAX) NULL,
    ValidationNotes   NVARCHAR(MAX) NULL,
    EmailSent         BIT           NOT NULL,
    IsDeleted         BIT           NOT NULL CONSTRAINT DF_Cases_IsDeleted DEFAULT (0),
    [RowVersion]      ROWVERSION    NOT NULL,
    CONSTRAINT PK_Cases PRIMARY KEY (Id)
);
GO

-- CaseNumber is unique across live and soft-deleted rows, so numbers are never
-- reused; 0 is excluded because legacy rows hold it until the app's startup
-- backfill numbers them.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cases_CaseNumber' AND object_id = OBJECT_ID('dbo.Cases'))
CREATE UNIQUE NONCLUSTERED INDEX IX_Cases_CaseNumber ON dbo.Cases (CaseNumber) WHERE CaseNumber > 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cases_Date' AND object_id = OBJECT_ID('dbo.Cases'))
CREATE INDEX IX_Cases_Date ON dbo.Cases ([Date]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cases_Branch' AND object_id = OBJECT_ID('dbo.Cases'))
CREATE INDEX IX_Cases_Branch ON dbo.Cases (Branch);
GO

-- ----------------------------------------------------------------------------
-- 1.3  Audit trail  (mirrors AuditEntry.cs) — who did what, when
-- ----------------------------------------------------------------------------
IF OBJECT_ID('dbo.Audits', 'U') IS NULL
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
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Audits_TimestampUtc' AND object_id = OBJECT_ID('dbo.Audits'))
CREATE INDEX IX_Audits_TimestampUtc ON dbo.Audits (TimestampUtc);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Audits_CaseId' AND object_id = OBJECT_ID('dbo.Audits'))
CREATE INDEX IX_Audits_CaseId ON dbo.Audits (CaseId);
GO

-- ============================ END OF PART 1 =================================
-- A new database is now fully built. Continue to PART 3 to record the version.


-- ############################################################################
-- ############################################################################
-- ##                                                                        ##
-- ##   PART 2 — UPGRADE AN EXISTING DATABASE                                ##
-- ##   Every step is guarded, so this is safe to run repeatedly and safe    ##
-- ##   immediately after Part 1. Back up the database first.                ##
-- ##                                                                        ##
-- ############################################################################
-- ############################################################################

-- ----------------------------------------------------------------------------
-- 2.1  Cases.CaseNumber — human-facing incremental case number
-- ----------------------------------------------------------------------------
IF COL_LENGTH('dbo.Cases', 'CaseNumber') IS NULL
BEGIN
    ALTER TABLE dbo.Cases
        ADD CaseNumber INT NOT NULL CONSTRAINT DF_Cases_CaseNumber DEFAULT 0;
END
GO

-- Number anything still at 0, oldest case date first (ties broken by Id, a
-- unix-ms timestamp, so creation order), continuing from the highest number in
-- use. The app's startup backfill does the same for rows added later.
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
-- 2.2  Branch managers  (mirror BranchManager.cs / BranchManagerAssignment.cs)
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
-- 2.3  Soft delete and optimistic concurrency on Cases
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
-- 2.4  Unique case numbers
--      If duplicates exist (only possible on databases numbered by a very
--      early script), the index is skipped and the message below explains the
--      fix; the version rows in Part 3 are then not written, so the app's
--      startup check keeps pointing here until it is resolved.
-- ----------------------------------------------------------------------------
IF EXISTS (SELECT CaseNumber FROM dbo.Cases WHERE CaseNumber > 0
           GROUP BY CaseNumber HAVING COUNT(*) > 1)
BEGIN
    PRINT '!! Duplicate CaseNumber values exist — unique index NOT created.';
    PRINT '!! Fix by renumbering all cases, then re-run this part:';
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
-- 2.5  Drop the unused customer-phone index (nothing queries by phone in SQL)
-- ----------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cases_CustomerPhone'
           AND object_id = OBJECT_ID('dbo.Cases'))
BEGIN
    DROP INDEX IX_Cases_CustomerPhone ON dbo.Cases;
END
GO

-- ----------------------------------------------------------------------------
-- 2.6  Audit trail  (mirrors AuditEntry.cs)
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

-- ============================ END OF PART 2 =================================


-- ############################################################################
-- ############################################################################
-- ##                                                                        ##
-- ##   PART 3 — SCHEMA VERSION (run after Part 1 or Part 2)                 ##
-- ##   The app reads this table at startup to confirm the database matches  ##
-- ##   the code. Version rows are written only when the unique case-number  ##
-- ##   index exists, i.e. when every step above actually completed.         ##
-- ##                                                                        ##
-- ############################################################################
-- ############################################################################

-- SchemaVersions  (mirrors SchemaVersion.cs)
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

    PRINT 'SQPortal database is at schema version 006.';
END
ELSE
BEGIN
    PRINT '!! Schema version NOT recorded — a step above did not complete.';
    PRINT '!! Read the messages printed by Part 2 and re-run it.';
END
GO

-- ============================================================================
-- Done. Start the app; a critical line in the log about the schema version
-- means a step above was skipped.
-- ============================================================================
