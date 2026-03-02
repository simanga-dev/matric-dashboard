import { v } from 'convex/values'
import { internalMutation } from './_generated/server'

export const clearMarks = internalMutation({
  args: {},
  returns: v.object({ deleted: v.number(), hasMore: v.boolean() }),
  handler: async (ctx) => {
    const batch = await ctx.db.query('marks').take(1000)
    for (const doc of batch) {
      await ctx.db.delete(doc._id)
    }
    return { deleted: batch.length, hasMore: batch.length === 1000 }
  },
})

export const clearSchools = internalMutation({
  args: {},
  returns: v.object({ deleted: v.number(), hasMore: v.boolean() }),
  handler: async (ctx) => {
    const batch = await ctx.db.query('school').take(1000)
    for (const doc of batch) {
      await ctx.db.delete(doc._id)
    }
    return { deleted: batch.length, hasMore: batch.length === 1000 }
  },
})

const recordValidator = v.object({
  province: v.string(),
  district_name: v.string(),
  emis_number: v.string(),
  centre_number: v.string(),
  centre_name: v.string(),
  quintile: v.string(),
  progressed_number_2022: v.string(),
  total_wrote_2022: v.string(),
  total_achieved_2022: v.string(),
  percent_achieved_2022: v.string(),
  progressed_number_2023: v.string(),
  total_wrote_2023: v.string(),
  total_achieved_2023: v.string(),
  percent_achieved_2023: v.string(),
  progressed_number_2024: v.string(),
  total_wrote_2024: v.string(),
  total_achieved_2024: v.string(),
  percent_achieved_2024: v.string(),
  progressed_number_2025: v.string(),
  total_wrote_2025: v.string(),
  total_achieved_2025: v.string(),
  percent_achieved_2025: v.string(),
})

export const insertBatch = internalMutation({
  args: { records: v.array(recordValidator) },
  returns: v.null(),
  handler: async (ctx, args) => {
    for (const record of args.records) {
      const quintile = Number(record.quintile) || undefined

      const schoolId = await ctx.db.insert('school', {
        natemis: Number(record.emis_number),
        centre_number: Number(record.centre_number),
        province: record.province,
        official_institution_name: record.centre_name,
        district_name: record.district_name,
        quantile: quintile,
      })

      const years = [
        {
          year: 2022,
          progressed: record.progressed_number_2022,
          wrote: record.total_wrote_2022,
          achieved: record.total_achieved_2022,
          percent: record.percent_achieved_2022,
        },
        {
          year: 2023,
          progressed: record.progressed_number_2023,
          wrote: record.total_wrote_2023,
          achieved: record.total_achieved_2023,
          percent: record.percent_achieved_2023,
        },
        {
          year: 2024,
          progressed: record.progressed_number_2024,
          wrote: record.total_wrote_2024,
          achieved: record.total_achieved_2024,
          percent: record.percent_achieved_2024,
        },
        {
          year: 2025,
          progressed: record.progressed_number_2025,
          wrote: record.total_wrote_2025,
          achieved: record.total_achieved_2025,
          percent: record.percent_achieved_2025,
        },
      ]

      for (const y of years) {
        const totalWrote = Number(y.wrote) || 0
        if (totalWrote === 0) continue

        await ctx.db.insert('marks', {
          school_id: schoolId,
          year: y.year,
          quantile: quintile,
          number_progressed: Number(y.progressed) || 0,
          total_wrote: totalWrote,
          total_achieved: Number(y.achieved) || 0,
          percentage_achieved: Number(y.percent) || 0,
        })
      }
    }

    return null
  },
})
