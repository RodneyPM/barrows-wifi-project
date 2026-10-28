-- 006 Stop duplicate connection events at the source (Ticket 2).
-- The portal retries on slow links, so the same event can reach the API more than once.
-- ConnectionEvents had no uniqueness on EventId, so every repeat was stored and counted.
--
-- 1. Keep the first copy of each EventId (lowest Id), set the repeats aside in an archive table
--    rather than destroying them, then
-- 2. make EventId unique so a repeat can never be stored again.
-- Verified on the seed data: the repeats are exact copies (same device, outlet, type and OccurredAt).

WITH ranked AS (
    SELECT *, ROW_NUMBER() OVER (PARTITION BY EventId ORDER BY Id) AS rn
    FROM dbo.ConnectionEvents
)
SELECT Id, EventId, OutletId, CampaignId, DeviceHash, EventType, OccurredAt, Msisdn, ReceivedAt,
       SYSDATETIMEOFFSET() AS ArchivedAt
INTO dbo.ConnectionEventDuplicates
FROM ranked
WHERE rn > 1;
GO

WITH ranked AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY EventId ORDER BY Id) AS rn
    FROM dbo.ConnectionEvents
)
DELETE FROM ranked WHERE rn > 1;
GO

CREATE UNIQUE INDEX UX_ConnectionEvents_EventId ON dbo.ConnectionEvents (EventId);
GO