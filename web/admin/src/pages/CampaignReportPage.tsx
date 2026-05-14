import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, type CampaignReport } from '../lib/api'
import { shortDate } from '../lib/format'
import { Card } from '../components/ui'

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <Card>
      <p className="text-sm text-slate-500">{label}</p>
      <p className="mt-1 text-2xl font-semibold">{value.toLocaleString('en-ZA')}</p>
    </Card>
  )
}

export default function CampaignReportPage() {
  const { id } = useParams()
  const [report, setReport] = useState<CampaignReport | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api<CampaignReport>(`/api/reports/campaigns/${id}/summary`)
      .then(setReport)
      .catch(() => setError('Could not load this report.'))
  }, [id])

  return (
    <div>
      <Link to="/campaigns" className="text-sm text-brand-600 hover:underline">
        ← All campaigns
      </Link>
      {error && <p className="mt-4 text-red-600">{error}</p>}
      {report && (
        <>
          <h1 className="mt-2 text-xl font-semibold">{report.campaignName}</h1>
          <p className="mb-6 text-sm text-slate-500">
            {report.organisation} · {shortDate(report.startsAt)} – {shortDate(report.endsAt)} · {report.outlets} outlets
          </p>
          <div className="grid gap-4 sm:grid-cols-3">
            <Stat label="Impressions (ad completes)" value={report.impressions} />
            <Stat label="Unique users" value={report.uniqueUsers} />
            <Stat label="Survey completes" value={report.surveyCompletes} />
          </div>
        </>
      )}
    </div>
  )
}
