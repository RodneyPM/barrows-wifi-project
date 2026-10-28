
import { Button, Input, Label } from '../../components/ui'
import type { CampaignDraft } from './campaignDraft'

interface Props {
  values: CampaignDraft
  onChange: (values: Partial<CampaignDraft>) => void
  onNext: () => void
}

export function StepDetails({ values, onChange, onNext }: Props) {
  return (
    <div className="space-y-4">
      <div>
        <Label htmlFor="name">Campaign name</Label>
        <Input
          id="name"
          value={values.name}
          onChange={(e) => onChange({ name: e.target.value })}
          placeholder="e.g. Festive Teaser"
        />
      </div>

      <div>
        <Label htmlFor="objective">Objective</Label>
        <textarea
          id="objective"
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
          rows={3}
          value={values.objective}
          onChange={(e) => onChange({ objective: e.target.value })}
          placeholder="What should this campaign achieve?"
        />
      </div>

      <div className="flex justify-end">
        <Button onClick={onNext}>Next</Button>
      </div>
    </div>
  )
}
