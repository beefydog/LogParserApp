USE [EmailLogDB]
GO
/****** Object:  Table [dbo].[LogEntries]    Script Date: 5/21/2024 7:53:16 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[LogEntries](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[ParsedLogId] [int] NULL,
	[LineNum] [int] NULL,
	[EntryDate] [datetime2](7) NULL,
	[IPaddress] [varchar](50) NULL,
	[Status] [nvarchar](20) NULL,
	[Action] [nvarchar](100) NULL,
	[Details] [nvarchar](500) NULL,
 CONSTRAINT [PK__LogEntri__3214EC079BECEAB0] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ParsedLogs]    Script Date: 5/21/2024 7:53:16 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ParsedLogs](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[FileName] [nvarchar](50) NULL,
	[LogType] [nvarchar](20) NULL,
	[LogFileId] [int] NULL,
	[DateParsed] [datetime2](7) NULL,
	[FileDate] [datetime2](7) NULL,
	[FileHash] [varchar](64) NULL,
 CONSTRAINT [PK__ParsedLo__3214EC07DC02269A] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ParsingErrors]    Script Date: 5/21/2024 7:53:16 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ParsingErrors](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[FileName] [nvarchar](50) NULL,
	[ErrorMessage] [nvarchar](1000) NULL
) ON [PRIMARY]
GO
