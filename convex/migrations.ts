import { internalMutation } from './_generated/server'
import { v } from 'convex/values'

export const fixMarksFieldNames = internalMutation({
  args: {},
  returns: v.object({ fixed: v.number(), hasMore: v.boolean() }),
  handler: async (ctx) => {
    const docs = await ctx.db.query('marks').take(1000)
    let fixed = 0
    for (const doc of docs) {
      const d = doc as any
      if (d.percentage_archived !== undefined || d.total_archived !== undefined) {
        await ctx.db.patch(doc._id, {
          percentage_achieved: d.percentage_archived ?? d.percentage_achieved,
          total_achieved: d.total_archived ?? d.total_achieved,
          percentage_archived: undefined,
          total_archived: undefined,
        } as any)
        fixed++
      }
    }
    return { fixed, hasMore: docs.length === 1000 }
  },
})
