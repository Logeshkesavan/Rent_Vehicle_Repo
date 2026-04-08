/*
 Post-deployment script - runs AFTER database updates
 Use this for:
 - Re-enabling foreign key constraints
 - Inserting seed/default data
 - Updating lookup tables
 - Data migrations
*/

-- Re-enable all foreign key constraints
EXEC sp_MSForEachTable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all'
GO

-- Example: Insert default/seed data
-- SET IDENTITY_INSERT dbo.Users ON
-- INSERT INTO dbo.Users (Id, Email, FullName, IsActive) 
-- VALUES (1, 'admin@example.com', 'Admin User', 1)
-- SET IDENTITY_INSERT dbo.Users OFF
-- GO

PRINT 'Post-deployment script completed'
GO
