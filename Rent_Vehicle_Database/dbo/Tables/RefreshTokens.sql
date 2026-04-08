CREATE TABLE [dbo].[RefreshTokens] (
    [Id]         INT             IDENTITY (1, 1) NOT NULL,
    [UserId]     INT             NOT NULL,
    [Token]      NVARCHAR (1000) NULL,
    [ExpiryDate] DATETIME2 (7)   NOT NULL,
    [IsRevoked]  BIT             NOT NULL,
    [CreatedAt]  DATETIME2 (7)   NOT NULL,
    [DeviceInfo] NVARCHAR (500)  NULL,
    [IpAddress]  NVARCHAR (50)   NULL,
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_RefreshTokens_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [IX_RefreshTokens_UserId]
    ON [dbo].[RefreshTokens]([UserId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_RefreshTokens_Token]
    ON [dbo].[RefreshTokens]([Token] ASC);

