-- ============================================================================
-- SQPortal — migration 006: per-case audit lookups
-- ============================================================================
-- Run this ONCE against an existing SQPortal database (SSMS) before starting
-- the app from this branch, after Scripts/005_future_proofing.sql. Not needed
-- on a database created with the current Scripts/init.sql.
--
-- The audit trail is now shown on each case record, so per-case lookups need
-- an index on Audits.CaseId.
--
-- Re-runnable: YES — every step is guarded.
-- ============================================================================

USE [SQPortal];
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Audits_CaseId' AND object_id = OBJECT_ID('dbo.Audits'))
BEGIN
    CREATE INDEX IX_Audits_CaseId ON dbo.Audits (CaseId);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE Version = '006')
BEGIN
    INSERT INTO dbo.SchemaVersions (Version) VALUES ('006');
END
GO
