CREATE TABLE [dbo].[Bookings] (
    [Id]              INT             IDENTITY (1, 1) NOT NULL,
    [UserId]          INT             NOT NULL,
    [VehicleId]       INT             NOT NULL,
    [PickupDate]      DATETIME2 (7)   NOT NULL,
    [ReturnDate]      DATETIME2 (7)   NOT NULL,
    [TotalDays]       INT             NOT NULL,
    [RentalAmount]    DECIMAL (10, 2) NOT NULL,
    [SecurityDeposit] DECIMAL (10, 2) NOT NULL,
    [TotalAmount]     DECIMAL (10, 2) NOT NULL,
    [Status]          NVARCHAR (50)   DEFAULT ('Pending') NOT NULL,
    [PaymentStatus]   NVARCHAR (50)   DEFAULT ('Pending') NOT NULL,
    [CreatedAt]       DATETIME2 (7)   DEFAULT (getdate()) NOT NULL,
    [UpdatedAt]       DATETIME2 (7)   DEFAULT (getdate()) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Bookings_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]),
    CONSTRAINT [FK_Bookings_Vehicles] FOREIGN KEY ([VehicleId]) REFERENCES [dbo].[Vehicles] ([Id])
);

