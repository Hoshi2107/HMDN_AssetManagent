UPDATE MaintenanceLogs SET Cost = 1500000 WHERE Cost IS NULL AND Status = 'closed'; UPDATE Inventories SET UnitPrice = 12500000, TotalPrice = 12500000 WHERE UnitPrice = 0 OR UnitPrice IS NULL;
