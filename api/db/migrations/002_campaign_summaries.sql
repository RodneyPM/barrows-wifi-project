-- 002 Post-campaign summaries (Kestrel Digital, KES-212, partially done)

CREATE TABLE CampaignSummaries (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    CampaignId       INT NOT NULL REFERENCES Campaigns(Id),
    GeneratedAt      DATETIMEOFFSET NOT NULL,
    Impressions      INT NOT NULL,
    UniqueUsers      INT NOT NULL,
    SurveyCompletes  INT NOT NULL
);
GO

-- Local stand-in for the mail relay. The FakeReportMailer writes every "sent" email here.
CREATE TABLE SentEmails (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    ToAddress   NVARCHAR(256)  NOT NULL,
    Subject     NVARCHAR(400)  NOT NULL,
    Body        NVARCHAR(MAX)  NOT NULL,
    SentAt      DATETIMEOFFSET NOT NULL
);
GO
