-- 005 Add Region to ConnectionEvents for the provincial dashboard (KES-240)
-- Prepared by Kestrel Digital. Scheduled to ship with the next production release.
-- Production ConnectionEvents is roughly 180 million rows.

BEGIN TRANSACTION;

ALTER TABLE ConnectionEvents ADD Region NVARCHAR(50) NULL;
GO

UPDATE ce
SET    ce.Region = o.Province
FROM   ConnectionEvents ce
JOIN   Outlets o ON o.Id = ce.OutletId;
GO

ALTER TABLE ConnectionEvents ALTER COLUMN Region NVARCHAR(50) NOT NULL;
GO

COMMIT;
GO
