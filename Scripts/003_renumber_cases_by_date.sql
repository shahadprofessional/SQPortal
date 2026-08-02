-- ============================================================================
-- SQPortal — migration 003: renumber cases in date order
-- ============================================================================
-- Run this ONCE if you ran the first version of Scripts/002_add_case_number.sql,
-- which numbered existing cases by Id (creation order) instead of by case date.
-- The symptom: the case IDs look shuffled when the list is sorted by date.
--
-- This renumbers EVERY case, oldest case date first, ties broken by Id (a
-- unix-ms timestamp, so Id order is creation order) — matching the order the
-- lists display. Numbers stay 1..N with no gaps.
--
-- Case numbers are display values only; nothing references them as a key, so
-- renumbering is safe. The NVARCHAR Id (the primary key behind every link)
-- is not touched.
--
-- Re-runnable: YES — it is idempotent, running it twice produces the same
-- numbers. Not needed on a database created from the current init.sql.
-- ============================================================================

USE [SQPortal];
GO

;WITH numbered AS
(
    SELECT CaseNumber,
           ROW_NUMBER() OVER (ORDER BY [Date], Id) AS rn
    FROM dbo.Cases
)
UPDATE numbered
SET CaseNumber = rn;
GO

-- Check the result: numbers should climb with the dates.
SELECT CaseNumber, [Date], CustomerName, Branch
FROM dbo.Cases
ORDER BY CaseNumber;
GO
