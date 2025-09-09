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
    quantile: v.number(),
    centre_number: v.number(),
    province: v.string(),
    official_institution_name: v.string(),
    district_name: v.string(),
  }).index('natemis', ['natemis']),

  marks: defineTable({
    year: v.number(),
    school_id: v.id('school'),
    number_progressed: v.number(),
    total_wrote: v.number(),
    total_archived: v.number(),
    percentage_archived: v.number(),
  }),
})
