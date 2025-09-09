import { createFileRoute } from '@tanstack/react-router'
import * as React from 'react'
import { useAction } from 'convex/react'
import { api } from 'convex/_generated/api'

export const Route = createFileRoute('/dashboard/seed-data')({
  component: RouteComponent,
})

function RouteComponent() {
  const parseCsv = useAction(api.seedDevData.seedFromFile)
  const [file, setFile] = React.useState<File | null>(null)
  const [status, setStatus] = React.useState<string>('')

  const onFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0] ?? null
    setFile(f)
    setStatus('')
  }

  const onUpload = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!file) {
      setStatus('Please select a CSV file first')
      return
    }
    try {
      setStatus('Reading file...')
      const text = await file.text()
      setStatus('Uploading to server and parsing...')
      const res = await parseCsv({ csvText: text })
      setStatus(`Done. Processed ${res?.rows ?? 0} rows. Check server logs.`)
    } catch (err) {
      console.error(err)
      setStatus('Failed to upload/parse CSV. See console for details.')
    }
  }

  return (
    <div className="p-6 space-y-4">
      <h1 className="text-xl font-semibold">Seed Data: Upload CSV</h1>
      <form onSubmit={onUpload} className="flex items-center gap-3">
        <input
          type="file"
          accept=".csv,text/csv"
          onChange={onFileChange}
          aria-label="Choose CSV file"
        />
        <button
          type="submit"
          disabled={!file}
          className="px-3 py-2 rounded disabled:opacity-50 bg-primary text-primary-foreground border border-border"
        >
          Upload & Parse
        </button>
      </form>
      {status ? <p className="text-sm opacity-80">{status}</p> : null}
    </div>
  )
}
