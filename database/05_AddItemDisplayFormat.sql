-- Displayed Excel text is already stored in Text. These columns keep layout and font for a replica grid.

IF COL_LENGTH(N'dbo.Items', N'ExcelRow') IS NULL
    ALTER TABLE [dbo].[Items] ADD [ExcelRow] INT NULL;
GO
IF COL_LENGTH(N'dbo.Items', N'ExcelColumn') IS NULL
    ALTER TABLE [dbo].[Items] ADD [ExcelColumn] INT NULL;
GO
IF COL_LENGTH(N'dbo.Items', N'IsBold') IS NULL
    ALTER TABLE [dbo].[Items] ADD [IsBold] BIT NOT NULL CONSTRAINT [DF_Items_IsBold] DEFAULT (0);
GO
IF COL_LENGTH(N'dbo.Items', N'FontColor') IS NULL
    ALTER TABLE [dbo].[Items] ADD [FontColor] NVARCHAR(9) NULL;
GO
IF COL_LENGTH(N'dbo.Items', N'FillColor') IS NULL
    ALTER TABLE [dbo].[Items] ADD [FillColor] NVARCHAR(9) NULL;
GO
