import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../../lib/api'
import { Button, Card } from '../../components/ui'
import { emptyDraft, toRequest, validateCampaign, type CampaignDraft } from './campaignDraft'
import { StepDetails } from './StepDetails'
import { StepSchedule } from './StepSchedule'
import { StepOutlets } from './StepOutlets'
import { StepReview } from './StepReview'

const STEPS = ['Details', 'Schedule & budget', 'Outlets', 'Review']

export function CampaignWizard() {
  const navigate = useNavigate()
  const [step, setStep] = useState(0)
  const [draft, setDraft] = useState<CampaignDraft>(emptyDraft)
  const [errors, setErrors] = useState<string[]>([])
  const [submitError, setSubmitError] = useState<string | null>(null)

  function next(values: Partial<CampaignDraft>) {
    setDraft({ ...draft, ...values })
    setStep(step + 1)
  }

  function back() {
    setStep(step - 1)
  }

  async function submit() {
    const problems = validateCampaign(draft)
    if (problems.length > 0) {
      setErrors(problems)
      return
    }
    setErrors([])
    setSubmitError(null)
    try {
      const created = await api<{ id: number }>('/api/campaigns', {
        method: 'POST',
        body: JSON.stringify(toRequest(draft)),
      })
      navigate(`/campaigns/${created.id}`)
    } catch {
      setSubmitError('Something went wrong. Please try again.')
    }
  }

  return (
    <div className="space-y-4">
      <ol className="flex gap-6 text-sm">
        {STEPS.map((label, i) => (
          <li key={label} className={i === step ? 'font-semibold text-brand-700' : 'text-slate-400'}>
            {i + 1}. {label}
          </li>
        ))}
      </ol>

      <Card>
        {step === 0 && <StepDetails onNext={next} />}
        {step === 1 && <StepSchedule onNext={next} onBack={back} />}
        {step === 2 && <StepOutlets onNext={next} onBack={back} />}
        {step === 3 && (
          <>
            {errors.length > 0 && (
              <ul className="mb-4 list-disc rounded-md bg-red-50 p-3 pl-8 text-sm text-red-700">
                {errors.map((e) => (
                  <li key={e}>{e}</li>
                ))}
              </ul>
            )}
            {submitError && <p className="mb-4 text-sm text-red-600">{submitError}</p>}
            <StepReview draft={draft} />
            <div className="mt-6 flex justify-between">
              <Button variant="secondary" onClick={back}>
                Back
              </Button>
              <Button onClick={submit}>Create campaign</Button>
            </div>
          </>
        )}
      </Card>
    </div>
  )
}
