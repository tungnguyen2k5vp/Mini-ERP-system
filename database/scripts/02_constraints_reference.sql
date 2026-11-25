-- Read-only constraint inventory.
SELECT SCHEMA_NAME(t.schema_id) AS SchemaName, t.name AS TableName, ck.name, ck.definition
FROM sys.check_constraints AS ck
JOIN sys.tables AS t ON t.object_id = ck.parent_object_id
WHERE SCHEMA_NAME(t.schema_id) IN ('auth', 'audit', 'catalog', 'inventory')
ORDER BY SchemaName, TableName, ck.name;

SELECT OBJECT_SCHEMA_NAME(fk.parent_object_id) AS SchemaName, fk.name,
       OBJECT_NAME(fk.parent_object_id) AS DependentTable,
       OBJECT_NAME(fk.referenced_object_id) AS PrincipalTable, fk.delete_referential_action_desc
FROM sys.foreign_keys AS fk
WHERE OBJECT_SCHEMA_NAME(fk.parent_object_id) IN ('auth', 'audit', 'catalog', 'inventory')
ORDER BY SchemaName, DependentTable, fk.name;
