-- Ticket 2: why does campaign 1 report ~4,700 ad completes when the agency's log says ~3,600?
-- Run these against the BarrowsWifi database BEFORE migration 006 is applied (006 removes the duplicates).
-- Change @c to look at another campaign.

DECLARE @c INT = 1;
DECLARE @s DATETIMEOFFSET, @e DATETIMEOFFSET;
SELECT @s = StartsAt, @e = EndsAt FROM Campaigns WHERE Id = @c;

-- 1. Reproduce what the report does today: every ad_complete at the campaign's outlets in its flight window.
SELECT COUNT(*) AS ImpressionsTodayReport
FROM ConnectionEvents e
JOIN CampaignOutlets co ON co.OutletId = e.OutletId
WHERE co.CampaignId = @c AND e.EventType = 'ad_complete'
  AND e.OccurredAt >= @s AND e.OccurredAt < @e;

-- 2. Whose events are those? Anything not tagged with this campaign belongs to someone else.
SELECT e.CampaignId, COUNT(*) AS AdCompletes
FROM ConnectionEvents e
JOIN CampaignOutlets co ON co.OutletId = e.OutletId
WHERE co.CampaignId = @c AND e.EventType = 'ad_complete'
  AND e.OccurredAt >= @s AND e.OccurredAt < @e
GROUP BY e.CampaignId;

-- 3. Which campaigns share outlets with this one?
SELECT other.CampaignId, COUNT(*) AS SharedOutlets
FROM CampaignOutlets mine
JOIN CampaignOutlets other ON other.OutletId = mine.OutletId AND other.CampaignId <> mine.CampaignId
WHERE mine.CampaignId = @c
GROUP BY other.CampaignId;

-- 4. Duplicate events: same EventId stored more than once (a retry the server also received).
SELECT COUNT(*) AS EventIdsStoredMoreThanOnce, SUM(n - 1) AS SurplusRows
FROM (SELECT EventId, COUNT(*) AS n FROM ConnectionEvents GROUP BY EventId HAVING COUNT(*) > 1) d;

-- 5. Are the repeats exact copies? (0 rows = yes, identical apart from when they arrived)
SELECT EventId
FROM ConnectionEvents
GROUP BY EventId
HAVING COUNT(*) > 1
   AND (COUNT(DISTINCT OccurredAt) > 1 OR COUNT(DISTINCT DeviceHash) > 1
        OR COUNT(DISTINCT OutletId) > 1 OR COUNT(DISTINCT EventType) > 1);

-- 6. The corrected number: this campaign's own events, each EventId once.
SELECT COUNT(DISTINCT EventId) AS ImpressionsCorrected
FROM ConnectionEvents
WHERE CampaignId = @c AND EventType = 'ad_complete'
  AND OccurredAt >= @s AND OccurredAt < @e;