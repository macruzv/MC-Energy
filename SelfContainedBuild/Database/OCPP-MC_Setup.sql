USE [master]
GO
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'OCPP-MC')
BEGIN
    CREATE DATABASE [OCPP-MC]
END
GO
USE [OCPP-MC]
GO

/****** Object:  Table [dbo].[ChargePoint] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ChargePoint](
	[ChargePointId] [nvarchar](100) NOT NULL,
	[Name] [nvarchar](100) NULL,
	[Comment] [nvarchar](200) NULL,
	[Username] [nvarchar](50) NULL,
	[Password] [nvarchar](50) NULL,
	[ClientCertThumb] [nvarchar](100) NULL,
 CONSTRAINT [PK_ChargePoint_1] PRIMARY KEY CLUSTERED ([ChargePointId] ASC)
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[ChargeTags] ******/
CREATE TABLE [dbo].[ChargeTags](
	[TagId] [nvarchar](50) NOT NULL,
	[TagName] [nvarchar](200) NULL,
	[ParentTagId] [nvarchar](50) NULL,
	[ExpiryDate] [datetime2](7) NULL,
	[Blocked] [bit] NULL,
 CONSTRAINT [PK_ChargeKeys] PRIMARY KEY CLUSTERED ([TagId] ASC)
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[MessageLog] ******/
CREATE TABLE [dbo].[MessageLog](
	[LogId] [int] IDENTITY(1,1) NOT NULL,
	[LogTime] [datetime2](7) NOT NULL,
	[ChargePointId] [nvarchar](100) NOT NULL,
	[ConnectorId] [int] NULL,
	[Message] [nvarchar](100) NOT NULL,
	[Result] [nvarchar](max) NULL,
	[ErrorCode] [nvarchar](100) NULL,
 CONSTRAINT [PK_MessageLog] PRIMARY KEY CLUSTERED ([LogId] ASC)
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

/****** Object:  Table [dbo].[Transactions] ******/
CREATE TABLE [dbo].[Transactions](
	[TransactionId] [int] IDENTITY(1,1) NOT NULL,
	[Uid] [nvarchar](50) NULL,
	[ChargePointId] [nvarchar](100) NOT NULL,
	[ConnectorId] [int] NOT NULL,
	[StartTagId] [nvarchar](50) NULL,
	[StartTime] [datetime2](7) NOT NULL,
	[MeterStart] [float] NOT NULL,
	[StartResult] [nvarchar](100) NULL,
	[StopTagId] [nvarchar](50) NULL,
	[StopTime] [datetime2](7) NULL,
	[MeterStop] [float] NULL,
	[StopReason] [nvarchar](100) NULL,
 CONSTRAINT [PK_Transactions] PRIMARY KEY CLUSTERED ([TransactionId] ASC)
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[ConnectorStatus] ******/
CREATE TABLE [dbo].[ConnectorStatus](
	[ChargePointId] [nvarchar](100) NOT NULL,
	[ConnectorId] [int] NOT NULL,
	[ConnectorName] [nvarchar](100) NULL,
	[LastStatus] [nvarchar](100) NULL,
	[LastStatusTime] [datetime2](7) NULL,
	[LastMeter] [float] NULL,
	[LastMeterTime] [datetime2](7) NULL,
 CONSTRAINT [PK_ConnectorStatus] PRIMARY KEY CLUSTERED ([ChargePointId] ASC, [ConnectorId] ASC)
) ON [PRIMARY]
GO

/****** Object:  View [dbo].[ConnectorStatusView] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[ConnectorStatusView]
AS
SELECT cs.ChargePointId, cs.ConnectorId, cs.ConnectorName, cs.LastStatus, cs.LastStatusTime, cs.LastMeter, cs.LastMeterTime, t.TransactionId, t.StartTagId, t.StartTime, t.MeterStart, t.StartResult, t.StopTagId, t.StopTime, t.MeterStop, 
                  t.StopReason
FROM     dbo.ConnectorStatus AS cs LEFT OUTER JOIN
                  dbo.Transactions AS t ON t.ChargePointId = cs.ChargePointId AND t.ConnectorId = cs.ConnectorId
WHERE  (t.TransactionId IS NULL) OR
                  (t.TransactionId IN
                      (SELECT MAX(TransactionId) AS Expr1
                       FROM      dbo.Transactions
                       GROUP BY ChargePointId, ConnectorId))
GO

/****** Indexing ******/
CREATE UNIQUE NONCLUSTERED INDEX [ChargePoint_Identifier] ON [dbo].[ChargePoint] ([ChargePointId] ASC)
GO
CREATE NONCLUSTERED INDEX [IX_MessageLog_ChargePointId] ON [dbo].[MessageLog] ([LogTime] ASC)
GO
ALTER TABLE [dbo].[Transactions]  WITH CHECK ADD  CONSTRAINT [FK_Transactions_ChargePoint] FOREIGN KEY([ChargePointId])
REFERENCES [dbo].[ChargePoint] ([ChargePointId])
GO
CREATE NONCLUSTERED INDEX [IX_Transactions_ChargePointId_ConnectorId] ON [dbo].[Transactions] ([ChargePointId] ASC, [ConnectorId] ASC)
GO

/****** Object:  Table [dbo].[__EFMigrationsHistory] ******/
CREATE TABLE [dbo].[__EFMigrationsHistory](
	[MigrationId] [nvarchar](150) NOT NULL,
	[ProductVersion] [nvarchar](32) NOT NULL,
 CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY CLUSTERED ([MigrationId] ASC)
) ON [PRIMARY]
GO
INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20240405204318_TransactionsIndex', '8.0.3')
GO

/****** Sample Data ******/
INSERT INTO [dbo].[ChargePoint] ([ChargePointId], [Name], [Comment]) VALUES ('station42', 'Test Station 42', 'Sample station for testing')
GO
INSERT INTO [dbo].[ChargeTags] ([TagId], [TagName], [Blocked]) VALUES ('tag123', 'Default Tag', 0)
GO

PRINT 'Database setup completed successfully.'
