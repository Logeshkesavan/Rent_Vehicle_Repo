/*
 Pre-deployment script - runs BEFORE database updates
 Use this for:
 - Disabling foreign key constraints
 - Dropping objects that will be recreated
 - Backing up data before schema changes
*/

-- Disable all foreign key constraints temporarily
EXEC sp_MSForEachTable 'ALTER TABLE ? NOCHECK CONSTRAINT all'
GO

-- Example: Drop stored procedure if it exists (will be recreated)
-- IF OBJECT_ID('dbo.spGetVehicles') IS NOT NULL
--     DROP PROCEDURE dbo.spGetVehicles
-- GO

PRINT 'Pre-deployment script completed'
GO
