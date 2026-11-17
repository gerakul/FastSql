-- Ported from Examples/InitQuery.sql: the tables, rows and procedure used by the README examples.
-- Batches are separated by GO lines (split by SqlServerDatabase).

CREATE TABLE [dbo].[Company](
	[ID] [int] NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[DateOfFoundation] [date] NOT NULL,
	CONSTRAINT [PK_Company] PRIMARY KEY CLUSTERED ([ID] ASC)
)
GO

INSERT INTO [dbo].[Company] (ID, Name, DateOfFoundation)
VALUES (1, 'Mega Co', '1990-06-08')

INSERT INTO [dbo].[Company] (ID, Name, DateOfFoundation)
VALUES (2, 'Oldest Co', '1820-08-09')
GO

CREATE TABLE [dbo].[Employee](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[CompanyID] [int] NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[Phone] [nvarchar](50) NULL,
	[Age] [int] NULL,
	[StartWorking] [datetime] NULL,
	CONSTRAINT [PK_Employee] PRIMARY KEY CLUSTERED ([ID] ASC)
)
GO

INSERT INTO [dbo].[Employee] (CompanyID, Name, Phone, Age, StartWorking)
VALUES (1, 'John Smith', '123-123-123', 46, NULL)

INSERT INTO [dbo].[Employee] (CompanyID, Name, Phone, Age, StartWorking)
VALUES (1, 'Anton Ivanov', NULL, NULL, '2006-01-20')

INSERT INTO [dbo].[Employee] (CompanyID, Name, Phone, Age, StartWorking)
VALUES (1, 'Engelbert Kaufmann', '222-333-444', 23, '2010-01-20')

INSERT INTO [dbo].[Employee] (CompanyID, Name, Phone, Age, StartWorking)
VALUES (2, 'Ivan Antonov', '123-456-789', 40, '2008-02-22')

INSERT INTO [dbo].[Employee] (CompanyID, Name, Phone, Age, StartWorking)
VALUES (2, 'Kate Donahue', '111-999-888', 26, NULL)
GO

CREATE PROCEDURE [dbo].[TestProc]
	@CompanyID int,
	@Age int
AS
BEGIN
	SET NOCOUNT ON;

	select * from Employee where CompanyID = @CompanyID and Age > @Age
END
GO
