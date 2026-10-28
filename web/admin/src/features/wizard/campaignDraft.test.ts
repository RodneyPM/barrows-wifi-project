
import { describe, expect, it } from 'vitest'
import {
  emptyDraft,
  sastDateToISOString,
  toRequest,
  validateCampaign,
  validateCampaignStep,
  type CampaignDraft,
} from './campaignDraft'

const validDraft: CampaignDraft = {
  ...emptyDraft,
  name: '  October campaign  ',
  startDate: '2026-10-01',
  endDate: '2026-10-31',
  budget: '10,000',
  outletIds: [1, 2],
}

describe('SAST date conversion', () => {
  it('converts the start date to midnight SAST', () => {
    expect(sastDateToISOString('2026-10-01')).toBe(
      '2026-09-30T22:00:00.000Z',
    )
  })

  it('makes the selected end date exclusive at the following midnight SAST', () => {
    expect(sastDateToISOString('2026-10-31', true)).toBe(
      '2026-10-31T22:00:00.000Z',
    )
  })

  it('handles the end of a year', () => {
    expect(sastDateToISOString('2026-12-31', true)).toBe(
      '2026-12-31T22:00:00.000Z',
    )
  })

  it('rejects impossible calendar dates', () => {
    expect(() => sastDateToISOString('2026-02-30')).toThrow(RangeError)
  })
})

describe('toRequest', () => {
  it('trims the name, parses budget and converts the date boundaries', () => {
    expect(toRequest(validDraft)).toEqual({
      name: 'October campaign',
      startsAt: '2026-09-30T22:00:00.000Z',
      endsAt: '2026-10-31T22:00:00.000Z',
      budgetZar: 10000,
      outletIds: [1, 2],
    })
  })
})

describe('campaign validation', () => {
  it('reports missing details on the details step', () => {
    expect(validateCampaignStep(emptyDraft, 0)).toContain(
      'Name is required',
    )
  })

  it('reports schedule errors on the schedule step', () => {
    expect(validateCampaignStep(emptyDraft, 1)).toEqual([
      'Start date is required',
      'End date is required',
      'Budget is required',
    ])
  })

  it('reports outlet errors on the outlet step', () => {
    expect(validateCampaignStep(emptyDraft, 2)).toEqual([
      'Select at least one outlet',
    ])
  })

  it('rejects an end date before the start date', () => {
    const draft = {
      ...validDraft,
      endDate: '2026-09-30',
    }

    expect(validateCampaignStep(draft, 1)).toContain(
      'End date must be on or after start date',
    )
  })

  it('accepts a valid draft', () => {
    expect(validateCampaign(validDraft)).toEqual([])
  })
})
