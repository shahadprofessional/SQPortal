-- ============================================================================
-- SQPortal — migration 004: branch managers
-- ============================================================================
-- Run this ONCE against an existing SQPortal database (SSMS) before starting
-- the app from this branch. Not needed on a database created with the
-- current Scripts/init.sql, which already includes both tables.
--
-- Adds the branch side of a case: a roster of branch managers, and one row per
-- branch saying who runs it. Mirrors Partners / BranchAssignments exactly.
-- Nothing existing is modified — no rows are touched, no columns change.
--
-- Both tables start empty; add managers and assign them in Settings.
--
-- Re-runnable: YES — both steps are guarded.
-- ============================================================================

USE [SQPortal];
GO

-- ============================================================================
-- Managers  (mirrors BranchManager.cs)
-- ============================================================================
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

-- ============================================================================
-- ManagerAssignments  (mirrors BranchManagerAssignment.cs)
-- ============================================================================
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
