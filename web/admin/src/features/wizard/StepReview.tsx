import { zar } from '../../lib/format'
import type { CampaignDraft } from './campaignDraft'

export function StepReview({ draft }: { draft: CampaignDraft }) {
  return (
    <dl className="grid grid-cols-3 gap-y-3 text-sm">
      <dt className="text-slate-500">Name</dt>
      <dd className="col-span-2">{draft.name || '—'}</dd>
      <dt className="text-slate-500">Objective</dt>
      <dd className="col-span-2">{draft.objective || '—'}</dd>
      <dt className="text-slate-500">Flight</dt>
      <dd className="col-span-2">
        {draft.startDate || '—'} to {draft.endDate || '—'}
      </dd>
      <dt className="text-slate-500">Budget</dt>
      <dd className="col-span-2">{draft.budget ? zar.format(parseInt(draft.budget)) : '—'}</dd>
      <dt className="text-slate-500">Outlets</dt>
      <dd className="col-span-2">{draft.outletIds.length}</dd>
    </dl>
  )
}
