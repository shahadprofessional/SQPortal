-- ============================================================================
-- SQPortal — migration 002: incremental case numbers
-- ============================================================================
-- Run this ONCE against an existing SQPortal database (SSMS) before starting
-- the app from this branch. Not needed on a database created with the
-- current Scripts/init.sql, which already includes the column.
--
-- Adds Cases.CaseNumber (1, 2, 3, …) — the human-facing case ID shown in the
-- lists. The NVARCHAR Id stays the primary key and keeps driving URLs.
-- Existing rows are numbered oldest case date first, ties broken by Id (a
-- unix-ms timestamp, so Id order is creation order).
--
-- If an earlier copy of this script numbered cases by Id alone, run
-- Scripts/003_renumber_cases_by_date.sql to put the numbers in date order.
--
-- Re-runnable: YES — both steps are guarded.
-- ============================================================================

USE [SQPortal];
GO

IF COL_LENGTH('dbo.Cases', 'CaseNumber') IS NULL
BEGIN
    ALTER TABLE dbo.Cases
        ADD CaseNumber INT NOT NULL CONSTRAINT DF_Cases_CaseNumber DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cases_CaseNumber' AND object_id = OBJECT_ID('dbo.Cases'))
BEGIN
    CREATE INDEX IX_Cases_CaseNumber ON dbo.Cases (CaseNumber);
END
GO

-- Number anything still sitting at 0, continuing from the highest number in use.
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

-- The app also backfills on startup (SQPortalDbContext.BackfillCaseNumbers),
-- so running this is only needed to get the column in place.
