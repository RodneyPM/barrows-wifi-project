-- KES-212: add a 'Failed' delivery status.
-- 'Failed' means the mail relay explicitly REFUSED the message, so nothing was sent and it is safe to retry.
-- (Unknown still means "no acknowledgement; it may have been sent", which is never retried automatically.)

ALTER TABLE dbo.CampaignEmailDeliveries
    DROP CONSTRAINT CK_CampaignEmailDeliveries_Status;
GO

ALTER TABLE dbo.CampaignEmailDeliveries
    ADD CONSTRAINT CK_CampaignEmailDeliveries_Status
        CHECK (Status IN ('Pending', 'Sending', 'Sent', 'Unknown', 'Failed'));
GO