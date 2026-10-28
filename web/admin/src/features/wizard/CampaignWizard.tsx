
import { useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../../lib/api'
import { Button, Card } from '../../components/ui'
import {
  emptyDraft,
  toRequest,
  validateCampaignStep,
  type CampaignDraft,
  type CampaignStep,
} from './campaignDraft'
import { StepDetails } from './StepDetails'
import { StepSchedule } from './StepSchedule'
import { StepOutlets } from './StepOutlets'
import { StepReview } from './StepReview'

const STEPS = ['Details', 'Schedule & budget', 'Outlets', 'Review']

export function CampaignWizard() {
  const navigate = useNavigate()
  const [step, setStep] = useState<CampaignStep>(0)
  const [draft, setDraft] = useState<CampaignDraft>({ ...emptyDraft })
  const [errors, setErrors] = useState<string[]>([])
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  // Synchronous guard prevents two clicks before React rerenders.
  const submissionInProgress = useRef(false)

  function updateDraft(values: Partial<CampaignDraft>) {
    setDraft((current) => ({ ...current, ...values }))
    setErrors([])
    setSubmitError(null)
  }

  function next() {
    const problems = validateCampaignStep(draft, step)

    if (problems.length > 0) {
      setErrors(problems)
      return
    }

    setErrors([])
    setStep((current) => Math.min(current + 1, 3) as CampaignStep)
  }

  function back() {
    setErrors([])
    setStep((current) => Math.max(current - 1, 0) as CampaignStep)
  }

  async function submit() {
    if (submissionInProgress.current) return

    const problems = validateCampaignStep(draft, 3)

    if (problems.length > 0) {
      const firstInvalidStep = ([0, 1, 2] as const).find(
        (candidate) =>
          validateCampaignStep(draft, candidate).length > 0,
      )

      setErrors(
        firstInvalidStep === undefined
          ? problems
          : validateCampaignStep(draft, firstInvalidStep),
      )

      if (firstInvalidStep !== undefined) {
        setStep(firstInvalidStep)
      }

      return
    }

    submissionInProgress.current = true
    setIsSubmitting(true)
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
    } finally {
      submissionInProgress.current = false
      setIsSubmitting(false)
    }
  }

  return (
    <div className="space-y-4">
      <ol className="flex flex-wrap gap-x-4 gap-y-2 text-xs sm:text-sm">
        {STEPS.map((label, i) => (
          <li
            key={label}
            aria-current={i === step ? 'step' : undefined}
            className={
              i === step
                ? 'font-semibold text-brand-700'
                : 'text-slate-400'
            }
          >
            {i + 1}. {label}
          </li>
        ))}
      </ol>

      <Card>
        {errors.length > 0 && (
          <ul
            role="alert"
            className="mb-4 list-disc rounded-md bg-red-50 p-3 pl-8 text-sm text-red-700"
          >
            {errors.map((error) => (
              <li key={error}>{error}</li>
            ))}
          </ul>
        )}

        {step === 0 && (
          <StepDetails
            values={draft}
            onChange={updateDraft}
            onNext={next}
          />
        )}

        {step === 1 && (
          <StepSchedule
            values={draft}
            onChange={updateDraft}
            onNext={next}
            onBack={back}
          />
        )}

        {step === 2 && (
          <StepOutlets
            selected={draft.outletIds}
            onSelectionChange={(outletIds) =>
              updateDraft({ outletIds })
            }
            onNext={next}
            onBack={back}
          />
        )}

        {step === 3 && (
          <>
            {submitError && (
              <p role="alert" className="mb-4 text-sm text-red-600">
                {submitError}
              </p>
            )}

            <StepReview draft={draft} />

            <div className="mt-6 flex justify-between gap-3">
              <Button
                variant="secondary"
                onClick={back}
                disabled={isSubmitting}
              >
                Back
              </Button>

              <Button
                onClick={submit}
                disabled={isSubmitting}
              >
                {isSubmitting ? 'Creating…' : 'Create campaign'}
              </Button>
            </div>
          </>
        )}
      </Card>
    </div>
  )
}
