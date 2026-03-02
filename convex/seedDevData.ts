'use node'
import { v } from 'convex/values'
import { action } from './_generated/server'
import { promises as fs } from 'fs'
import os from 'os'
import { parse } from 'csv-parse'
import { internal } from './_generated/api'

// Parses CSV text and inserts data into the school and marks tables
export const seedFromFile = action({
  args: {
    csvText: v.string(),
  },
  returns: v.object({
    success: v.boolean(),
    recordsInserted: v.number(),
  }),
  handler: async (ctx, args) => {
    // Clear existing data in batches
    let hasMore = true
    while (hasMore) {
      const result = await ctx.runMutation(internal.seedMutations.clearMarks)
      hasMore = result.hasMore
    }
    hasMore = true
    while (hasMore) {
      const result = await ctx.runMutation(internal.seedMutations.clearSchools)
      hasMore = result.hasMore
    }

    await fs.writeFile(`${os.tmpdir()}/input.csv`, args.csvText, 'utf8')

    const content = await fs.readFile(`${os.tmpdir()}/input.csv`)
    const parser = parse(content, {
      skip_empty_lines: true,
      trim: true,
      columns: true,
    })

    const allRecords: Array<{
      province: string
      district_name: string
      emis_number: string
      centre_number: string
      centre_name: string
      quintile: string
      progressed_number_2022: string
      total_wrote_2022: string
      total_achieved_2022: string
      percent_achieved_2022: string
      progressed_number_2023: string
      total_wrote_2023: string
      total_achieved_2023: string
      percent_achieved_2023: string
      progressed_number_2024: string
      total_wrote_2024: string
      total_achieved_2024: string
      percent_achieved_2024: string
      progressed_number_2025: string
      total_wrote_2025: string
      total_achieved_2025: string
      percent_achieved_2025: string
    }> = []

    for await (const record of parser) {
      allRecords.push({
        province: String(record.province ?? ''),
        district_name: String(record.district_name ?? ''),
        emis_number: String(record.emis_number ?? ''),
        centre_number: String(record.centre_number ?? ''),
        centre_name: String(record.centre_name ?? ''),
        quintile: String(record.quintile ?? ''),
        progressed_number_2022: String(record.progressed_number_2022 ?? ''),
        total_wrote_2022: String(record.total_wrote_2022 ?? ''),
        total_achieved_2022: String(record.total_achieved_2022 ?? ''),
        percent_achieved_2022: String(record.percent_achieved_2022 ?? ''),
        progressed_number_2023: String(record.progressed_number_2023 ?? ''),
        total_wrote_2023: String(record.total_wrote_2023 ?? ''),
        total_achieved_2023: String(record.total_achieved_2023 ?? ''),
        percent_achieved_2023: String(record.percent_achieved_2023 ?? ''),
        progressed_number_2024: String(record.progressed_number_2024 ?? ''),
        total_wrote_2024: String(record.total_wrote_2024 ?? ''),
        total_achieved_2024: String(record.total_achieved_2024 ?? ''),
        percent_achieved_2024: String(record.percent_achieved_2024 ?? ''),
        progressed_number_2025: String(record.progressed_number_2025 ?? ''),
        total_wrote_2025: String(record.total_wrote_2025 ?? ''),
        total_achieved_2025: String(record.total_achieved_2025 ?? ''),
        percent_achieved_2025: String(record.percent_achieved_2025 ?? ''),
      })
    }

    const BATCH_SIZE = 100
    for (let i = 0; i < allRecords.length; i += BATCH_SIZE) {
      const batch = allRecords.slice(i, i + BATCH_SIZE)
      await ctx.runMutation(internal.seedMutations.insertBatch, {
        records: batch,
      })
    }

    return { success: true, recordsInserted: allRecords.length }
  },
})
