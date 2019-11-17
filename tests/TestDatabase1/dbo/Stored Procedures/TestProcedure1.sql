CREATE PROCEDURE [dbo].[TestProcedure1]
AS
	SELECT * FROM [$(TestDatabase2)].dbo.TestTable;
RETURN 0
