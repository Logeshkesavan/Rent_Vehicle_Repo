CREATE TABLE [dbo].[Vehicles] (
    [Id]                 INT             IDENTITY (1, 1) NOT NULL,
    [Category]           NVARCHAR (50)   NOT NULL,
    [Type]               NVARCHAR (50)   NOT NULL,
    [Brand]              NVARCHAR (100)  NOT NULL,
    [Model]              NVARCHAR (100)  NOT NULL,
    [Year]               INT             NOT NULL,
    [RegistrationNumber] NVARCHAR (50)   NOT NULL,
    [FuelType]           NVARCHAR (50)   NOT NULL,
    [Transmission]       NVARCHAR (50)   NULL,
    [SeatingCapacity]    INT             NOT NULL,
    [PricePerDay]        DECIMAL (10, 2) NOT NULL,
    [PricePerHour]       DECIMAL (10, 2) NULL,
    [Location]           NVARCHAR (200)  NOT NULL,
    [AvailabilityStatus] NVARCHAR (50)   DEFAULT ('Available') NOT NULL,
    [Images]             NVARCHAR (MAX)  NULL,
    [Features]           NVARCHAR (MAX)  NULL,
    [IsActive]           BIT             DEFAULT ((1)) NOT NULL,
    [CreatedAt]          DATETIME2 (7)   DEFAULT (getdate()) NOT NULL,
    [UpdatedAt]          DATETIME2 (7)   DEFAULT (getdate()) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    UNIQUE NONCLUSTERED ([RegistrationNumber] ASC)
);

