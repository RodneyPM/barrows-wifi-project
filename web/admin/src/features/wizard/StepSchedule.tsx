
import { Button, Input, Label } from '../../components/ui'
import type { CampaignDraft } from './campaignDraft'

interface Props {
  values: CampaignDraft
  onChange: (values: Partial<CampaignDraft>) => void
  onNext: () => void
  onBack: () => void
}

export function StepSchedule({
  values,
  onChange,
  onNext,
  onBack,
}: Props) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-4">
        <div>
          <Label htmlFor="start">First day</Label>
          <Input
            id="start"
            type="date"
            value={values.startDate}
            onChange={(e) => onChange({ startDate: e.target.value })}
          />
        </div>

        <div>
          <Label htmlFor="end">Last day</Label>
          <Input
            id="end"
            type="date"
            min={values.startDate || undefined}
            value={values.endDate}
            onChange={(e) => onChange({ endDate: e.target.value })}
          />
        </div>
      </div>

      <div>
        <Label htmlFor="budget">Budget (ZAR)</Label>
        <Input
          id="budget"
          inputMode="decimal"
          value={values.budget}
          onChange={(e) => onChange({ budget: e.target.value })}
          placeholder="e.g. 150 000"
        />
      </div>

      <div className="flex justify-between">
        <Button variant="secondary" onClick={onBack}>
          Back
        </Button>
        <Button onClick={onNext}>Next</Button>
      </div>
    </div>
  )
}
