import { v } from 'convex/values'
import { internalMutation } from './_generated/server'

export const insertRecords = internalMutation({
  args: {
    centre_number: v.string(),
    province: v.string(),
    district_name: v.string(),
    emis_number: v.string(),
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
    count: v.int64(),
  },

  handler: async (ctx, args) => {
    const schoolId = await ctx.db.insert('school', {
      natemis: Number(args.emis_number),
      quantile: Number(args.quintile),
      centre_number: Number(args.centre_number),
      province: args.province,
      official_institution_name: args.centre_name,
      district_name: args.district_name,
    })

    await ctx.db.insert('marks', {
      school_id: schoolId,
      year: 2023,
      number_progressed: Number(args.progressed_number_2023),
      total_wrote: Number(args.total_wrote_2023),
      total_archived: Number(args.total_achieved_2023),
      percentage_archived: Number(args.percent_achieved_2023),
    })

    return { schoolsInserted: args.count }
  },
})
