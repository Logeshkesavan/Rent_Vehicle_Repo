CREATE TABLE [dbo].[Users] (
    [Id]           INT            IDENTITY (1, 1) NOT NULL,
    [Name]         NVARCHAR (100) NOT NULL,
    [Email]        NVARCHAR (255) NOT NULL,
    [PasswordHash] NVARCHAR (500) NOT NULL,
    [Phone]        NVARCHAR (20)  NULL,
    [Role]         NVARCHAR (20)  DEFAULT ('Customer') NOT NULL,
    [IsActive]     BIT            DEFAULT ((1)) NOT NULL,
    [CreatedAt]    DATETIME2 (7)  DEFAULT (getdate()) NOT NULL,
    [UpdatedAt]    DATETIME2 (7)  DEFAULT (getdate()) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    UNIQUE NONCLUSTERED ([Email] ASC)
);

