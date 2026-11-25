-- reporting.usp_GetInventoryReport returns total count, then the requested page.
EXEC reporting.usp_GetInventoryReport @LowOnly = 1, @Offset = 0, @PageSize = 15;
