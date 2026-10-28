
import { useEffect, useState } from 'react'
import { api, type Outlet, type Paged } from '../../lib/api'
import { Button, Input } from '../../components/ui'
import {
  addOutletSelections,
  buildOutletSearchPath,
  OUTLET_PAGE_SIZE,
  toggleOutletSelection,
} from './outletSearch'

interface Props {
  selected: number[]
  onSelectionChange: (ids: number[]) => void
  onNext: () => void
  onBack: () => void
}

export function StepOutlets({
  selected,
  onSelectionChange,
  onNext,
  onBack,
}: Props) {
  const [filter, setFilter] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [result, setResult] = useState<Paged<Outlet> | null>(null)
  const [loading, setLoading] = useState(false)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)

  // Debounce search so slow connections don't receive a request per keystroke.
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setSearch(filter.trim())
      setPage(1)
    }, 300)

    return () => window.clearTimeout(timer)
  }, [filter])

  // Fetch only the current page. Ignore responses for outdated searches/pages.
  useEffect(() => {
    let active = true

    setLoading(true)
    setLoadError(null)

    api<Paged<Outlet>>(buildOutletSearchPath(search, page))
      .then((data) => {
        if (active) setResult(data)
      })
      .catch(() => {
        if (active) setLoadError('Could not load outlets. Please try again.')
      })
      .finally(() => {
        if (active) setLoading(false)
      })

    return () => {
      active = false
    }
  }, [search, page, retry])

  const outlets = result?.items ?? []
  const total = result?.total ?? 0
  const pageSize = result?.pageSize ?? OUTLET_PAGE_SIZE
  const totalPages = Math.max(1, Math.ceil(total / pageSize))

  function toggle(id: number) {
    onSelectionChange(toggleOutletSelection(selected, id))
  }

  function selectAllShown() {
    onSelectionChange(
      addOutletSelections(selected, outlets.map((outlet) => outlet.id)),
    )
  }

  return (
    <div className="space-y-4">
      <div className="space-y-2 sm:flex sm:items-center sm:gap-3 sm:space-y-0">
        <div className="min-w-0 flex-1">
          <label
            htmlFor="outlet-search"
            className="mb-1 block text-sm font-medium text-slate-700"
          >
            Search outlets
          </label>
          <Input
            placeholder="Name, STP number or province"
            value={filter}
            onChange={(event) => setFilter(event.target.value)}
          />
        </div>

        <Button
          variant="secondary"
          onClick={selectAllShown}
          disabled={outlets.length === 0 || loading}
        >
          Select page
        </Button>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-slate-600">
        <p>{selected.length} selected</p>
        <p>{total.toLocaleString()} matching outlets</p>
      </div>

      {loadError && (
        <div className="space-y-2 rounded-md border border-red-200 p-3">
          <p role="alert" className="text-sm text-red-700">
            {loadError}
          </p>
          <Button
            variant="secondary"
            onClick={() => setRetry((value) => value + 1)}
          >
            Retry
          </Button>
        </div>
      )}

      <div
        className="max-h-96 overflow-y-auto rounded-md border border-slate-200"
        aria-busy={loading}
      >
        {loading && (
          <p className="p-3 text-sm text-slate-500" role="status">
            Loading outlets…
          </p>
        )}

        {!loading && !loadError && outlets.length === 0 && (
          <p className="p-6 text-center text-sm text-slate-500">
            No matching outlets found.
          </p>
        )}

        {!loadError && (
          <ul className="divide-y divide-slate-100">
            {outlets.map((outlet) => (
              <li key={outlet.id} className="p-3">
                <label className="flex cursor-pointer items-start gap-3">
                  <input
                    type="checkbox"
                    className="mt-1 h-4 w-4 shrink-0"
                    checked={selected.includes(outlet.id)}
                    onChange={() => toggle(outlet.id)}
                    aria-label={`Select ${outlet.name}`}
                  />

                  <span className="min-w-0 flex-1">
                    <span className="block break-words font-medium text-slate-900">
                      {outlet.name}
                    </span>
                    <span className="mt-1 block break-words text-sm text-slate-600">
                      STP: {outlet.stpNumber}
                    </span>
                    <span className="block break-words text-sm text-slate-600">
                      {outlet.province} · Access point: {outlet.apStatus}
                    </span>
                  </span>
                </label>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <Button
          variant="secondary"
          onClick={() => setPage((current) => Math.max(1, current - 1))}
          disabled={page <= 1 || loading}
        >
          Previous
        </Button>

        <span className="text-sm text-slate-600" aria-live="polite">
          Page {page} of {totalPages}
        </span>

        <Button
          variant="secondary"
          onClick={() =>
            setPage((current) => Math.min(totalPages, current + 1))
          }
          disabled={page >= totalPages || loading}
        >
          Next
        </Button>
      </div>

      <div className="flex justify-between gap-3 border-t border-slate-200 pt-4">
        <Button variant="secondary" onClick={onBack}>
          Back
        </Button>
        <Button onClick={onNext}>Next step</Button>
      </div>
    </div>
  )
}
