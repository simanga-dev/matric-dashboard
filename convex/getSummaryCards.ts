import { query } from './_generated/server'

export const GetMatricPassRate = query({
  handler: async (ctx) => {
    try {
      const marks = await ctx.db.query('marks').collect()

      console.log('Total Marks:', marks)

      return marks
    } catch (error) {
      console.error('Error while fetching marks:', error)
      return null
    }
  },
})
