import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type CampaignListItem, type Paged } from '../lib/api'
import { useAuth } from '../lib/auth'
import { shortDate, zar } from '../lib/format'
import { Button, StatusBadge } from '../components/ui'

export default function CampaignsPage() {
  const { user } = useAuth()
  const [data, setData] = useState<Paged<CampaignListItem> | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api<Paged<CampaignListItem>>('/api/campaigns?page=1&pageSize=50')
      .then(setData)
      .catch(() => setError('Could not load campaigns.'))
  }, [])

  const canCreate = user?.role === 'PlatformAdmin' || user?.role === 'ClientAdmin'

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-xl font-semibold">Campaigns</h1>
        {canCreate && (
          <Link to="/campaigns/new">
            <Button>New campaign</Button>
          </Link>
        )}
      </div>

      {error && <p className="text-red-600">{error}</p>}
      {!data && !error && <p className="text-slate-500">Loading…</p>}

      {data && (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
          <table className="min-w-full text-sm">
            <thead className="bg-slate-50 text-left text-slate-600">
              <tr>
                <th className="px-4 py-2">Campaign</th>
                <th className="px-4 py-2">Client</th>
                <th className="px-4 py-2">Status</th>
                <th className="px-4 py-2">Flight</th>
                <th className="px-4 py-2 text-right">Outlets</th>
                <th className="px-4 py-2 text-right">Budget</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((c) => (
                <tr key={c.id} className="border-t border-slate-100 hover:bg-slate-50">
                  <td className="px-4 py-2">
                    <Link to={`/campaigns/${c.id}`} className="font-medium text-brand-600 hover:underline">
                      {c.name}
                    </Link>
                  </td>
                  <td className="px-4 py-2">{c.organisation}</td>
                  <td className="px-4 py-2">
                    <StatusBadge status={c.status} />
                  </td>
                  <td className="whitespace-nowrap px-4 py-2">
                    {shortDate(c.startsAt)} – {shortDate(c.endsAt)}
                  </td>
                  <td className="px-4 py-2 text-right">{c.outletCount}</td>
                  <td className="px-4 py-2 text-right">{zar.format(c.budgetZar)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
