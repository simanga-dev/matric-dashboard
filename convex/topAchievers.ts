import { query } from './_generated/server'
import { v } from 'convex/values'

export const getByYear = query({
  args: { year: v.number() },
  returns: v.array(
    v.object({
      _id: v.id('top_achievers'),
      _creationTime: v.number(),
      year: v.number(),
      name: v.string(),
      school_name: v.string(),
      province: v.string(),
      percentage_mark: v.number(),
      headshot_url: v.optional(v.string()),
      rank: v.number(),
    })
  ),
  handler: async (ctx, args) => {
    const achievers = await ctx.db
      .query('top_achievers')
      .withIndex('by_year', (q) => q.eq('year', args.year))
      .collect()

    return achievers.sort((a, b) => a.rank - b.rank)
  },
})

export const getAvailableYears = query({
  args: {},
  returns: v.array(v.number()),
  handler: async (ctx) => {
    const achievers = await ctx.db.query('top_achievers').collect()
    const years = [...new Set(achievers.map((a) => a.year))]
    return years.sort((a, b) => b - a)
  },
})
