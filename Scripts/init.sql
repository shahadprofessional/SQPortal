-- ============================================================================
-- SQPortal — schema initialization (fresh database)          schema version 006
-- ============================================================================
-- Run ONCE against a NEW EMPTY database from SSMS. For an existing database
-- created from an earlier version of these scripts, run upgrade.sql instead.
-- See Scripts/README.md.
--
-- After running, start the app: SeedLookups() (Data/SQPortalDbContext.cs)
-- populates the default partners, branches and branch-partner assignments.
--
-- This script is the SOURCE OF TRUTH for the schema; the application code
-- creates no tables. Keep it in sync with the entity classes in
-- Models/Entities/ and with Data/SQPortalDbContext.cs (OnModelCreating).
--
-- Re-runnable on an existing schema: NO. Designed for a fresh empty database.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 0. Database (optional). Skip when "SQPortal" already exists.
-- ----------------------------------------------------------------------------
-- CREATE DATABASE SQPortal;
-- GO

USE [SQPortal];
GO

-- ----------------------------------------------------------------------------
-- 1. Lookup tables: people and branches
-- ----------------------------------------------------------------------------

-- SQ staff  (mirrors BusinessPartner.cs)
CREATE TABLE dbo.Partners
(
    Name      NVARCHAR(40)  NOT NULL,
    FullName  NVARCHAR(200) NOT NULL,
    Email     NVARCHAR(200) NOT NULL,
    CONSTRAINT PK_Partners PRIMARY KEY (Name)
);
GO

-- Branches  (mirrors Branch.cs)
CREATE TABLE dbo.Branches
(
    Name NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_Branches PRIMARY KEY (Name)
);
GO

-- Branch managers  (mirrors BranchManager.cs)
CREATE TABLE dbo.Managers
(
    Name      NVARCHAR(40)  NOT NULL,
    FullName  NVARCHAR(200) NOT NULL,
    Email     NVARCHAR(200) NOT NULL,
    CONSTRAINT PK_Managers PRIMARY KEY (Name)
);
GO

-- Which SQ staff member handles each branch  (mirrors BranchPartnerAssignment.cs)
CREATE TABLE dbo.BranchAssignments
(
    BranchName       NVARCHAR(100) NOT NULL,
    AssignedPartner  NVARCHAR(40)  NOT NULL,
    CONSTRAINT PK_BranchAssignments PRIMARY KEY (BranchName)
);
GO

-- Which manager runs each branch  (mirrors BranchManagerAssignment.cs)
CREATE TABLE dbo.ManagerAssignments
(
    BranchName       NVARCHAR(100) NOT NULL,
    AssignedManager  NVARCHAR(40)  NOT NULL,
    CONSTRAINT PK_ManagerAssignments PRIMARY KEY (BranchName)
);
GO

-- ----------------------------------------------------------------------------
-- 2. Cases  (mirrors FeedbackCase.cs)
--    RootCauses is JSON text via an EF value converter, hence NVARCHAR(MAX).
--    FollowUpStatus and CaseValidation are enums stored as int values.
--    IsDeleted is the soft-delete flag; RowVersion the concurrency token.
-- ----------------------------------------------------------------------------
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

-- CaseNumber is unique across live and soft-deleted rows, so numbers are
-- never reused; 0 is excluded because legacy rows hold it until the app's
-- startup backfill numbers them.
CREATE UNIQUE NONCLUSTERED INDEX IX_Cases_CaseNumber ON dbo.Cases (CaseNumber) WHERE CaseNumber > 0;
GO

CREATE INDEX IX_Cases_Date   ON dbo.Cases ([Date]);
GO

CREATE INDEX IX_Cases_Branch ON dbo.Cases (Branch);
GO

-- ----------------------------------------------------------------------------
-- 3. Audit trail  (mirrors AuditEntry.cs)
-- ----------------------------------------------------------------------------
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

CREATE INDEX IX_Audits_TimestampUtc ON dbo.Audits (TimestampUtc);
GO

CREATE INDEX IX_Audits_CaseId ON dbo.Audits (CaseId);
GO

-- ----------------------------------------------------------------------------
-- 4. Schema version tracking  (mirrors SchemaVersion.cs; checked at startup)
-- ----------------------------------------------------------------------------
CREATE TABLE dbo.SchemaVersions
(
    Version       NVARCHAR(20) NOT NULL,
    AppliedAtUtc  DATETIME2    NOT NULL CONSTRAINT DF_SchemaVersions_AppliedAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_SchemaVersions PRIMARY KEY (Version)
);
GO

INSERT INTO dbo.SchemaVersions (Version) VALUES ('005'), ('006');
GO

-- ============================================================================
-- Done. Start the app — SeedLookups() will populate the default rows.
-- ============================================================================
