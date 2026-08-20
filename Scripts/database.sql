
IF DB_ID('SQPortal') IS NULL
    CREATE DATABASE SQPortal;
GO

USE [SQPortal];
GO

IF OBJECT_ID('dbo.Partners', 'U') IS NULL
CREATE TABLE dbo.Partners
(
    Name      NVARCHAR(40)  NOT NULL,
    FullName  NVARCHAR(200) NOT NULL,
    Email     NVARCHAR(200) NOT NULL,
    CONSTRAINT PK_Partners PRIMARY KEY (Name)
);
GO

IF OBJECT_ID('dbo.Branches', 'U') IS NULL
CREATE TABLE dbo.Branches
(
    Name NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_Branches PRIMARY KEY (Name)
);
GO

-- Branch managers are a copy of Active Directory: the sync writes these rows,
-- the portal never edits them. Nobody is deleted when they stop managing —
-- IsActive goes to 0 — so past assignments still resolve to a name and address.
IF OBJECT_ID('dbo.Managers', 'U') IS NULL
CREATE TABLE dbo.Managers
(
    Name               NVARCHAR(40)  NOT NULL,
    UserPrincipalName  NVARCHAR(200) NOT NULL CONSTRAINT DF_Managers_UserPrincipalName DEFAULT (''),
    FullName           NVARCHAR(200) NOT NULL,
    Email              NVARCHAR(200) NOT NULL,
    IsActive           BIT           NOT NULL CONSTRAINT DF_Managers_IsActive DEFAULT (1),
    LastSyncedUtc      DATETIME2     NULL,
    CONSTRAINT PK_Managers PRIMARY KEY (Name)
);
GO

IF OBJECT_ID('dbo.BranchAssignments', 'U') IS NULL
CREATE TABLE dbo.BranchAssignments
(
    BranchName       NVARCHAR(100) NOT NULL,
    AssignedPartner  NVARCHAR(40)  NOT NULL,
    CONSTRAINT PK_BranchAssignments PRIMARY KEY (BranchName)
);
GO

-- One row per manager per spell running a branch. A move closes the old row
-- (EffectiveTo = the day before) and opens a new one, so who ran a branch on
-- any past date stays answerable.
IF OBJECT_ID('dbo.ManagerAssignments', 'U') IS NULL
CREATE TABLE dbo.ManagerAssignments
(
    BranchName       NVARCHAR(100) NOT NULL,
    AssignedManager  NVARCHAR(40)  NOT NULL,
    EffectiveFrom    DATE          NOT NULL CONSTRAINT DF_ManagerAssignments_EffectiveFrom DEFAULT ('19000101'),
    EffectiveTo      DATE          NULL,
    CONSTRAINT PK_ManagerAssignments PRIMARY KEY (BranchName, AssignedManager, EffectiveFrom)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ManagerAssignments_BranchName_EffectiveFrom'
               AND object_id = OBJECT_ID('dbo.ManagerAssignments'))
CREATE INDEX IX_ManagerAssignments_BranchName_EffectiveFrom
    ON dbo.ManagerAssignments (BranchName, EffectiveFrom);
GO


    
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



    
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cases_CaseNumber' AND object_id = OBJECT_ID('dbo.Cases'))
CREATE UNIQUE NONCLUSTERED INDEX IX_Cases_CaseNumber ON dbo.Cases (CaseNumber) WHERE CaseNumber > 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cases_Date' AND object_id = OBJECT_ID('dbo.Cases'))
CREATE INDEX IX_Cases_Date ON dbo.Cases ([Date]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cases_Branch' AND object_id = OBJECT_ID('dbo.Cases'))
CREATE INDEX IX_Cases_Branch ON dbo.Cases (Branch);
GO


    
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



    

    
    
    
IF COL_LENGTH('dbo.Cases', 'CaseNumber') IS NULL
BEGIN
    ALTER TABLE dbo.Cases
        ADD CaseNumber INT NOT NULL CONSTRAINT DF_Cases_CaseNumber DEFAULT 0;
END
GO

    
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

    /* ---- schema 007: branch managers come from Active Directory ---- */

    /* Identity fields the sync copies from AD. */
IF COL_LENGTH('dbo.Managers', 'UserPrincipalName') IS NULL
BEGIN
    ALTER TABLE dbo.Managers
        ADD UserPrincipalName NVARCHAR(200) NOT NULL
            CONSTRAINT DF_Managers_UserPrincipalName DEFAULT ('');
END
GO

IF COL_LENGTH('dbo.Managers', 'IsActive') IS NULL
BEGIN
    ALTER TABLE dbo.Managers
        ADD IsActive BIT NOT NULL CONSTRAINT DF_Managers_IsActive DEFAULT (1);
END
GO

IF COL_LENGTH('dbo.Managers', 'LastSyncedUtc') IS NULL
BEGIN
    ALTER TABLE dbo.Managers ADD LastSyncedUtc DATETIME2 NULL;
END
GO

    /*
      Assignments become dated. Rows that already exist are stamped
      1900-01-01, i.e. "has always been true": every report on a past period
      keeps naming the manager it named before this upgrade. From here on a
      manager's move closes their old branch's row and opens the new one.
    */
IF COL_LENGTH('dbo.ManagerAssignments', 'EffectiveFrom') IS NULL
BEGIN
    ALTER TABLE dbo.ManagerAssignments
        ADD EffectiveFrom DATE NOT NULL
                CONSTRAINT DF_ManagerAssignments_EffectiveFrom DEFAULT ('19000101'),
            EffectiveTo   DATE NULL;
END
GO

    /* The key becomes (branch, manager, start date) so a branch can hold history. */
IF NOT EXISTS (SELECT 1
               FROM sys.indexes i
               JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
               JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
               WHERE i.object_id = OBJECT_ID('dbo.ManagerAssignments')
                 AND i.is_primary_key = 1
                 AND c.name = 'EffectiveFrom')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.key_constraints
               WHERE name = 'PK_ManagerAssignments'
                 AND parent_object_id = OBJECT_ID('dbo.ManagerAssignments'))
    BEGIN
        ALTER TABLE dbo.ManagerAssignments DROP CONSTRAINT PK_ManagerAssignments;
    END

    ALTER TABLE dbo.ManagerAssignments
        ADD CONSTRAINT PK_ManagerAssignments
            PRIMARY KEY (BranchName, AssignedManager, EffectiveFrom);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ManagerAssignments_BranchName_EffectiveFrom'
               AND object_id = OBJECT_ID('dbo.ManagerAssignments'))
BEGIN
    CREATE INDEX IX_ManagerAssignments_BranchName_EffectiveFrom
        ON dbo.ManagerAssignments (BranchName, EffectiveFrom);
END
GO

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

        
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cases_CustomerPhone'
           AND object_id = OBJECT_ID('dbo.Cases'))
BEGIN
    DROP INDEX IX_Cases_CustomerPhone ON dbo.Cases;
END
GO


    
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

    /* 007 lands only once the AD-linked manager columns are really there. */
IF COL_LENGTH('dbo.ManagerAssignments', 'EffectiveFrom') IS NOT NULL
   AND COL_LENGTH('dbo.Managers', 'UserPrincipalName') IS NOT NULL
   AND COL_LENGTH('dbo.Managers', 'IsActive') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE Version = '007')
        INSERT INTO dbo.SchemaVersions (Version) VALUES ('007');

    PRINT 'SQPortal database is at schema version 007.';
END
ELSE
BEGIN
    PRINT '!! Schema version 007 NOT recorded — the branch-manager columns are missing.';
    PRINT '!! Re-run Part 2; the app logs a critical line and refuses to seed until this is done.';
END
GO

