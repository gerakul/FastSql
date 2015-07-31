------------------------------------------------------------------------------------------
------------------------------------------------------------------------------------------
------- YOU MUST CREATE DATABASES SampleDB1 AND SampleDB2 BEFORE EXECUTING THE SCRIPT ----
------------------------------------------------------------------------------------------
------------------------------------------------------------------------------------------

USE [SampleDB1]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Company](
	[ID] [int] NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[DateOfFoundation] [date] NOT NULL,
 CONSTRAINT [PK_Company] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

INSERT INTO [dbo].[Company] (ID, Name, DateOfFoundation)
VALUES (1, 'Mega Co', '1990-06-08')

INSERT INTO [dbo].[Company] (ID, Name, DateOfFoundation)
VALUES (2, 'Oldest Co', '1820-08-09')

GO


SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Employee](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[CompanyID] [int] NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[Phone] [nvarchar](50) NULL,
	[Age] [int] NULL,
	[StartWorking] [datetime] NULL,
 CONSTRAINT [PK_Employee] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

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

USE [SampleDB2]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Person](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](50) NULL,
	[Age] [int] NULL,
 CONSTRAINT [PK_Person] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

