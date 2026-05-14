import { useState } from 'react'
import { Button, Input, Label } from '../../components/ui'
import type { CampaignDraft } from './campaignDraft'

interface Props {
  onNext: (values: Partial<CampaignDraft>) => void
  onBack: () => void
}

export function StepSchedule({ onNext, onBack }: Props) {
  const [startDate, setStartDate] = useState('')
  const [endDate, setEndDate] = useState('')
  const [budget, setBudget] = useState('')

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-4">
        <div>
          <Label htmlFor="start">First day</Label>
          <Input id="start" type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
        </div>
        <div>
          <Label htmlFor="end">Last day</Label>
          <Input id="end" type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
        </div>
      </div>
      <div>
        <Label htmlFor="budget">Budget (ZAR)</Label>
        <Input id="budget" value={budget} onChange={(e) => setBudget(e.target.value)} placeholder="e.g. 150 000" />
      </div>
      <div className="flex justify-between">
        <Button variant="secondary" onClick={onBack}>
          Back
        </Button>
        <Button onClick={() => onNext({ startDate, endDate, budget })}>Next</Button>
      </div>
    </div>
  )
}
