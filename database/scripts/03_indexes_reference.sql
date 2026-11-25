-- Read-only index keys and columns.
SELECT SCHEMA_NAME(t.schema_id) AS SchemaName, t.name AS TableName, i.name AS IndexName,
       i.type_desc, i.is_unique, ic.key_ordinal, c.name AS ColumnName,
       ic.is_descending_key, ic.is_included_column, i.filter_definition
FROM sys.indexes AS i
JOIN sys.tables AS t ON t.object_id = i.object_id
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE SCHEMA_NAME(t.schema_id) IN ('auth', 'audit', 'catalog', 'inventory')
ORDER BY SchemaName, TableName, IndexName, ic.key_ordinal, ColumnName;
