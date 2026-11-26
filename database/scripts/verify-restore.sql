-- Run against MiniErp on the local developer instance with sqlcmd -b.
-- COPY_ONLY preserves the differential base. Restore always targets new database and file names.
SET NOCOUNT ON;
IF DB_NAME() <> N'MiniErp' OR CONVERT(nvarchar(128),SERVERPROPERTY('Edition')) NOT LIKE '%Developer%'
    THROW 51000, 'Select the MiniErp development database on Developer Edition.', 1;
IF (SELECT COUNT(*) FROM sys.database_files) <> 2
    THROW 51001, 'This drill expects one data file and one log file.', 1;
DECLARE @id varchar(32) = REPLACE(CONVERT(varchar(36),NEWID()),'-','');
DECLARE @target sysname = 'MiniErp_Test_Restore_' + @id;
DECLARE @path nvarchar(4000) = CONVERT(nvarchar(3500),SERVERPROPERTY('InstanceDefaultBackupPath'));
DECLARE @data nvarchar(4000) = CONVERT(nvarchar(3500),SERVERPROPERTY('InstanceDefaultDataPath'));
DECLARE @log nvarchar(4000) = CONVERT(nvarchar(3500),SERVERPROPERTY('InstanceDefaultLogPath'));
IF @path IS NULL OR @data IS NULL OR @log IS NULL THROW 51002, 'Default SQL directories unavailable.', 1;
DECLARE @sep nchar(1) = CASE WHEN @path LIKE '/%' THEN '/' ELSE '\' END;
SET @path = CONCAT(@path, CASE WHEN RIGHT(@path,1) <> @sep THEN @sep END, @target, '.bak');
SET @data = CONCAT(@data, CASE WHEN RIGHT(@data,1) <> @sep THEN @sep END, @target, '.mdf');
SET @log = CONCAT(@log, CASE WHEN RIGHT(@log,1) <> @sep THEN @sep END, @target, '.ldf');
DECLARE @logicalData sysname = (SELECT name FROM sys.database_files WHERE type = 0);
DECLARE @logicalLog sysname = (SELECT name FROM sys.database_files WHERE type = 1);
IF DB_ID(@target) IS NOT NULL THROW 51003, 'Restore target already exists.', 1;
BACKUP DATABASE MiniErp TO DISK = @path WITH COPY_ONLY, CHECKSUM, COMPRESSION;
RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM;
RESTORE DATABASE @target FROM DISK = @path WITH MOVE @logicalData TO @data, MOVE @logicalLog TO @log, CHECKSUM, RECOVERY;
DBCC CHECKDB(@target) WITH NO_INFOMSGS, ALL_ERRORMSGS;
DECLARE @check nvarchar(max) = 'SELECT DB_NAME() AS RestoredDatabase, (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory) AS Migrations,
    (SELECT COUNT_BIG(*) FROM catalog.Products) AS Products, (SELECT COUNT_BIG(*) FROM inventory.Movements) AS Movements';
DECLARE @exec nvarchar(260) = QUOTENAME(@target) + '.sys.sp_executesql';
EXEC @exec @check;
