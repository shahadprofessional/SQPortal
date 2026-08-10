-- ============================================================================
-- SQPortal — database schema initialization
-- ============================================================================
-- Run this script ONCE against an empty SQPortal database from SSMS.
-- After running, start the app; SeedLookups() (Data/SQPortalDbContext.cs)
-- will populate the 4 default partners, 27 branches, and 27 default
-- branch-partner assignments via plain INSERTs.
--
-- This script is the SOURCE OF TRUTH for the schema; the application code
-- creates no tables. Keep this file in sync with:
--   - Models/Entities/FeedbackCase.cs
--   - Models/Entities/BusinessPartner.cs
--   - Models/Entities/Branch.cs
--   - Models/Entities/BranchPartnerAssignment.cs
--   - Data/SQPortalDbContext.cs (OnModelCreating — indexes + value converters)
--
-- Re-runnable on an existing schema: NO. Designed for a fresh empty database.
-- ============================================================================

-- Step 1 (optional). Create the empty database. Skip when "SQPortal" already
-- exists (e.g. created via SSMS's New Database menu).
-- CREATE DATABASE SQPortal;
-- GO

USE [SQPortal];
GO

-- ============================================================================
-- Partners  (mirrors BusinessPartner.cs)
-- ============================================================================
CREATE TABLE dbo.Partners
(
    Name      NVARCHAR(40)  NOT NULL,
    FullName  NVARCHAR(200) NOT NULL,
    Email     NVARCHAR(200) NOT NULL,
    CONSTRAINT PK_Partners PRIMARY KEY (Name)
);
GO

-- ============================================================================
-- Branches  (mirrors Branch.cs)
-- ============================================================================
CREATE TABLE dbo.Branches
(
    Name NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_Branches PRIMARY KEY (Name)
);
GO

-- ============================================================================
-- BranchAssignments  (mirrors BranchPartnerAssignment.cs)
-- ============================================================================
CREATE TABLE dbo.BranchAssignments
(
    BranchName       NVARCHAR(100) NOT NULL,
    AssignedPartner  NVARCHAR(40)  NOT NULL,
    CONSTRAINT PK_BranchAssignments PRIMARY KEY (BranchName)
);
GO

-- ============================================================================
-- Managers  (mirrors BranchManager.cs) — the branch side of a case
-- ============================================================================
CREATE TABLE dbo.Managers
(
    Name      NVARCHAR(40)  NOT NULL,
    FullName  NVARCHAR(200) NOT NULL,
    Email     NVARCHAR(200) NOT NULL,
    CONSTRAINT PK_Managers PRIMARY KEY (Name)
);
GO

-- ============================================================================
-- ManagerAssignments  (mirrors BranchManagerAssignment.cs)
-- ============================================================================
CREATE TABLE dbo.ManagerAssignments
(
    BranchName       NVARCHAR(100) NOT NULL,
    AssignedManager  NVARCHAR(40)  NOT NULL,
    CONSTRAINT PK_ManagerAssignments PRIMARY KEY (BranchName)
);
GO

-- ============================================================================
-- Cases  (mirrors FeedbackCase.cs)
-- RootCauses is stored as JSON text via an EF Core value converter
-- (Data/SQPortalDbContext.cs::OnModelCreating). Hence NVARCHAR(MAX).
-- FollowUpStatus and CaseValidation are enums stored as their int values.
-- ============================================================================
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

-- ============================================================================
-- Indexes on Cases (match Data/SQPortalDbContext.cs::OnModelCreating)
-- CaseNumber is unique across live and soft-deleted rows; 0 is excluded
-- because legacy rows hold it until the app's startup backfill.
-- ============================================================================
CREATE UNIQUE NONCLUSTERED INDEX IX_Cases_CaseNumber ON dbo.Cases (CaseNumber) WHERE CaseNumber > 0;
GO

CREATE INDEX IX_Cases_CustomerPhone ON dbo.Cases (CustomerPhone);
GO

CREATE INDEX IX_Cases_Date          ON dbo.Cases ([Date]);
GO

CREATE INDEX IX_Cases_Branch        ON dbo.Cases (Branch);
GO

-- ============================================================================
-- Audits  (mirrors AuditEntry.cs) — who did what, when
-- ============================================================================
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

-- ============================================================================
-- SchemaVersions  (mirrors SchemaVersion.cs) — applied-script tracking,
-- checked at app startup. This script includes everything through 005.
-- ============================================================================
CREATE TABLE dbo.SchemaVersions
(
    Version       NVARCHAR(20) NOT NULL,
    AppliedAtUtc  DATETIME2    NOT NULL CONSTRAINT DF_SchemaVersions_AppliedAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_SchemaVersions PRIMARY KEY (Version)
);
GO

INSERT INTO dbo.SchemaVersions (Version) VALUES ('005');
GO

-- ============================================================================
-- Done. Start the app — SeedLookups() will populate the default rows.
-- ============================================================================
