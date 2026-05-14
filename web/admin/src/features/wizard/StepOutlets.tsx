import { useEffect, useState } from 'react'
import { api, type Outlet } from '../../lib/api'
import { Button, Input } from '../../components/ui'
import type { CampaignDraft } from './campaignDraft'

interface Props {
  onNext: (values: Partial<CampaignDraft>) => void
  onBack: () => void
}

export function StepOutlets({ onNext, onBack }: Props) {
  const [outlets, setOutlets] = useState<Outlet[]>([])
  const [filter, setFilter] = useState('')
  const [selected, setSelected] = useState<number[]>([])

  useEffect(() => {
    api<Outlet[]>('/api/outlets').then(setOutlets)
  }, [])

  const shown = outlets.filter(
    (o) =>
      o.name.toLowerCase().includes(filter.toLowerCase()) ||
      o.province.toLowerCase().includes(filter.toLowerCase()) ||
      o.stpNumber.includes(filter),
  )

  function toggle(id: number) {
    setSelected(selected.includes(id) ? selected.filter((s) => s !== id) : [...selected, id])
  }

  function selectAllShown() {
    setSelected([...selected, ...shown.map((o) => o.id)])
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <Input placeholder="Search name, province or STP" value={filter} onChange={(e) => setFilter(e.target.value)} />
        <Button variant="secondary" onClick={selectAllShown}>
          Select all shown
        </Button>
      </div>
      <p className="text-sm text-slate-600">
        {selected.length} selected · {shown.length} shown
      </p>
      <div className="max-h-96 overflow-y-auto rounded-md border border-slate-200">
        <table className="w-[900px] text-sm">
          <thead className="sticky top-0 bg-slate-50 text-left text-slate-600">
            <tr>
              <th className="px-3 py-2"></th>
              <th className="px-3 py-2">STP</th>
              <th className="px-3 py-2">Outlet</th>
              <th className="px-3 py-2">Province</th>
              <th className="px-3 py-2">Access point</th>
            </tr>
          </thead>
          <tbody>
            {shown.map((o) => (
              <tr key={o.id} className="border-t border-slate-100">
                <td className="px-3 py-1.5">
                  <input type="checkbox" checked={selected.includes(o.id)} onChange={() => toggle(o.id)} />
                </td>
                <td className="px-3 py-1.5 font-mono">{o.stpNumber}</td>
                <td className="px-3 py-1.5">{o.name}</td>
                <td className="px-3 py-1.5">{o.province}</td>
                <td className="px-3 py-1.5">{o.apStatus}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="flex justify-between">
        <Button variant="secondary" onClick={onBack}>
          Back
        </Button>
        <Button onClick={() => onNext({ outletIds: selected })}>Next</Button>
      </div>
    </div>
  )
}
