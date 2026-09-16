/*
    QuanLyKhachSan_ResetHost.sql
    Run this ONLY on the target deployment database before re-importing
    QuanLyKhachSan_FullDeploy.sql after a failed/partial import.

    The hosting database is selected by the host control panel/query window.
    Do not add USE [DatPhongKhachSan] here.
*/

SET NOCOUNT ON;

DECLARE @sql nvarchar(max) = N'';

SELECT @sql = @sql + N'ALTER TABLE '
    + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name)
    + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' + CHAR(13) + CHAR(10)
FROM sys.foreign_keys AS fk
JOIN sys.tables AS t ON fk.parent_object_id = t.object_id;

IF LEN(@sql) > 0
    EXEC sp_executesql @sql;

SET @sql = N'';

SELECT @sql = @sql + N'DROP TABLE '
    + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name)
    + N';' + CHAR(13) + CHAR(10)
FROM sys.tables
WHERE is_ms_shipped = 0;

IF LEN(@sql) > 0
    EXEC sp_executesql @sql;

PRINT N'Host database reset complete.';
