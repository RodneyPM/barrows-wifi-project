
export const OUTLET_PAGE_SIZE = 25

export function buildOutletSearchPath(
  search: string,
  page: number,
  pageSize = OUTLET_PAGE_SIZE,
): string {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  })

  const term = search.trim()
  if (term) params.set('search', term)

  return `/api/outlets?${params.toString()}`
}

export function toggleOutletSelection(
  selected: number[],
  id: number,
): number[] {
  return selected.includes(id)
    ? selected.filter((selectedId) => selectedId !== id)
    : [...selected, id]
}

export function addOutletSelections(
  selected: number[],
  ids: number[],
): number[] {
  return [...new Set([...selected, ...ids])]
}
