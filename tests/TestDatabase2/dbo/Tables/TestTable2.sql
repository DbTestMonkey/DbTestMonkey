CREATE TABLE [dbo].[TestTable] (
    [PK_Id] INT           IDENTITY (1, 1) NOT NULL,
    [Name]  NVARCHAR (50) NULL,
    CONSTRAINT [PK_TestTable] PRIMARY KEY CLUSTERED ([PK_Id] ASC)
);
