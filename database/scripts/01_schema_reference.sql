-- Run against MiniErp. EF Core migrations own DDL.
SELECT s.name AS SchemaName, t.name AS TableName, c.column_id, c.name AS ColumnName,
       ty.name AS DataType, c.max_length, c.precision, c.scale, c.is_nullable
FROM sys.tables AS t
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
INNER JOIN sys.columns AS c ON c.object_id = t.object_id
INNER JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
WHERE s.name IN ('auth', 'audit', 'catalog', 'inventory')
ORDER BY s.name, t.name, c.column_id;
