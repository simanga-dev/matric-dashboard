import { createFileRoute } from '@tanstack/react-router'
import * as React from 'react'

export const Route = createFileRoute('/dashboard/seed-data')({
  component: RouteComponent,
})

function RouteComponent() {
  const onUpload = async (e: React.FormEvent) => {
    e.preventDefault()
    console.log('Hello world')
  }

  return (
    <div className="p-6 space-y-4">
      <h1 className="text-xl font-semibold">Seed Data: Upload CSV</h1>
      <form onSubmit={onUpload} className="flex items-center gap-3">
        <input
          type="file"
          accept=".csv,text/csv"
          aria-label="Choose CSV file"
        />
        <button
          //
          type="submit"
          onClick={(e) => {
            e.preventDefault()
            onUpload(e) // Call your onSubmit handler
          }}
          className="px-3 py-2 rounded disabled:opacity-50"
        >
          Upload & Parse
        </button>
      </form>
    </div>
  )
}
