
import { describe, expect, it } from 'vitest'
import {
  addOutletSelections,
  buildOutletSearchPath,
  toggleOutletSelection,
} from './outletSearch'

describe('buildOutletSearchPath', () => {
  it('requests one page of outlets', () => {
    expect(buildOutletSearchPath('', 1)).toBe(
      '/api/outlets?page=1&pageSize=25',
    )
  })

  it('trims and encodes the search term', () => {
    expect(buildOutletSearchPath(' Gauteng & North ', 2)).toBe(
      '/api/outlets?page=2&pageSize=25&search=Gauteng+%26+North',
    )
  })

  it('supports a custom page size', () => {
    expect(buildOutletSearchPath('Sandton', 3, 50)).toBe(
      '/api/outlets?page=3&pageSize=50&search=Sandton',
    )
  })
})

describe('outlet selection', () => {
  it('adds an unselected outlet', () => {
    expect(toggleOutletSelection([1, 2], 3)).toEqual([1, 2, 3])
  })

  it('removes a selected outlet', () => {
    expect(toggleOutletSelection([1, 2, 3], 2)).toEqual([1, 3])
  })

  it('adds page selections without duplicates', () => {
    expect(addOutletSelections([1, 2], [2, 3, 4])).toEqual([1, 2, 3, 4])
  })
})
