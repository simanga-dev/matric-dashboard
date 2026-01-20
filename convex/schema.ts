import { defineSchema, defineTable } from 'convex/server'
import { v } from 'convex/values'

// The schema is entirely optional.
// You can delete this file (schema.ts) and the
// app will continue to work.
// The schema provides more precise TypeScript types.
export default defineSchema({
  numbers: defineTable({
    value: v.number(),
  }),

  school: defineTable({
    natemis: v.number(),
    centre_number: v.number(),
    province: v.string(),
    official_institution_name: v.string(),
    district_name: v.string(),
    quantile: v.optional(v.float64()),
  }).index('natemis', ['natemis']),

  marks: defineTable({
    year: v.number(),
    quantile: v.optional(v.float64()),
    school_id: v.id('school'),
    number_progressed: v.number(),
    total_wrote: v.number(),
    total_archived: v.number(),
    percentage_archived: v.number(),
  }),

  top_achievers: defineTable({
    year: v.number(),
    name: v.string(),
    school_name: v.string(),
    province: v.string(),
    percentage_mark: v.number(),
    headshot_url: v.optional(v.string()),
    rank: v.number(),
  }).index('by_year', ['year']),
})
