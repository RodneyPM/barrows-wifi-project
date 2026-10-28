
-- KES-212: prevent duplicate summaries and track email delivery attempts.

-- One summary per campaign.
CREATE UNIQUE INDEX UX_CampaignSummaries_CampaignId
    ON dbo.CampaignSummaries(CampaignId);
GO

-- One delivery record per campaign and recipient.
CREATE TABLE dbo.CampaignEmailDeliveries
(
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CampaignId      INT NOT NULL
        REFERENCES dbo.Campaigns(Id),
    ToAddress       NVARCHAR(256) NOT NULL,
    Status          NVARCHAR(20) NOT NULL,
    CreatedAt       DATETIMEOFFSET NOT NULL,
    AttemptedAt     DATETIMEOFFSET NULL,
    CompletedAt     DATETIMEOFFSET NULL,
    LastError       NVARCHAR(1000) NULL,

    CONSTRAINT CK_CampaignEmailDeliveries_Status
        CHECK (Status IN ('Pending', 'Sending', 'Sent', 'Unknown'))
);
GO

CREATE UNIQUE INDEX UX_CampaignEmailDeliveries_Campaign_Recipient
    ON dbo.CampaignEmailDeliveries(CampaignId, ToAddress);
GO
