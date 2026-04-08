CREATE TABLE [dbo].[Payments] (
    [Id]            INT             IDENTITY (1, 1) NOT NULL,
    [BookingId]     INT             NOT NULL,
    [UserId]        INT             NOT NULL,
    [Amount]        DECIMAL (10, 2) NOT NULL,
    [PaymentMethod] NVARCHAR (50)   NOT NULL,
    [TransactionId] NVARCHAR (200)  NULL,
    [Status]        NVARCHAR (50)   DEFAULT ('Pending') NOT NULL,
    [CreatedAt]     DATETIME2 (7)   DEFAULT (getdate()) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Payments_Bookings] FOREIGN KEY ([BookingId]) REFERENCES [dbo].[Bookings] ([Id]),
    CONSTRAINT [FK_Payments_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id])
);

