USE BarrowsWifi;
GO

-- 1. Duplicate summaries (expect 0 rows: one per campaign).
SELECT CampaignId, COUNT(*) AS SummaryCount
FROM dbo.CampaignSummaries
GROUP BY CampaignId
HAVING COUNT(*) > 1;
GO

-- 2. Indexes on CampaignSummaries and CampaignEmailDeliveries (expect unique indexes on the keys).
SELECT OBJECT_NAME(i.object_id) AS TableName,
       i.name AS IndexName,
       i.is_unique AS IsUnique,
       STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS IndexedColumns
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.object_id IN (OBJECT_ID('dbo.CampaignSummaries'), OBJECT_ID('dbo.CampaignEmailDeliveries'))
  AND i.is_hypothetical = 0
GROUP BY i.object_id, i.name, i.is_unique
ORDER BY TableName, IndexName;
GO

-- 3. The delivery ledger: what happened to each recipient.
SELECT CampaignId, ToAddress, Status, CreatedAt, AttemptedAt, CompletedAt, LastError
FROM dbo.CampaignEmailDeliveries
ORDER BY CampaignId, ToAddress;
GO

-- 4. PROOF OF "ONCE": no address got more than one email for the same campaign
--    (expect 0 rows). The relay stand-in records every email it really sent.
SELECT ToAddress, Subject, COUNT(*) AS EmailsReceived
FROM dbo.SentEmails
GROUP BY ToAddress, Subject
HAVING COUNT(*) > 1;
GO

-- 5. Every delivery that ended up Unknown, and whether the relay actually sent it
--    (with the local fake, Unknown + a SentEmails row means "sent, but the ack timed out").
SELECT d.ToAddress, d.Status, d.LastError,
       (SELECT COUNT(*) FROM dbo.SentEmails s WHERE s.ToAddress = d.ToAddress) AS RelayRecordedEmails
FROM dbo.CampaignEmailDeliveries d
WHERE d.Status IN ('Unknown', 'Sending', 'Failed');
GO