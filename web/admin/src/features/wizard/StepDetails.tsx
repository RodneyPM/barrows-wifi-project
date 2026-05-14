import { useState } from 'react'
import { Button, Input, Label } from '../../components/ui'
import type { CampaignDraft } from './campaignDraft'

export function StepDetails({ onNext }: { onNext: (values: Partial<CampaignDraft>) => void }) {
  const [name, setName] = useState('')
  const [objective, setObjective] = useState('')

  return (
    <div className="space-y-4">
      <div>
        <Label htmlFor="name">Campaign name</Label>
        <Input id="name" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Festive Teaser" />
      </div>
      <div>
        <Label htmlFor="objective">Objective</Label>
        <textarea
          id="objective"
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
          rows={3}
          value={objective}
          onChange={(e) => setObjective(e.target.value)}
          placeholder="What should this campaign achieve?"
        />
      </div>
      <div className="flex justify-end">
        <Button onClick={() => onNext({ name, objective })}>Next</Button>
      </div>
    </div>
  )
}
