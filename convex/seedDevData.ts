'use node'
import { v } from 'convex/values'
import { action } from './_generated/server'
import { promises as fs } from 'fs'
import os from 'os'
import { parse } from 'csv-parse'
import { insertRecords } from './seedMutations'
import { internal } from './_generated/api'

// Parses CSV text and inserts data into the school and marks tables
export const seedFromFile = action({
  args: {
    csvText: v.string(),
  },
  handler: async (ctx, args) => {
    await fs.writeFile(`${os.tmpdir()}/input.csv`, args.csvText, 'utf8')

    const content = await fs.readFile(`${os.tmpdir()}/input.csv`)
    const records = []
    const parser = parse(content, {
      skip_empty_lines: true,
      trim: true,
      columns: true,
    })

    let count = 0n

    for await (const record of parser) {
      const parsedRecord = {
        province: String(record.province),
        district_name: String(record.district_name),
        emis_number: String(record.emis_number),
        centre_number: String(record.centre_number),
        centre_name: String(record.centre_name),
        quintile: String(record.quintile),
        progressed_number_2022: String(record.progressed_number_2022),
        total_wrote_2022: String(record.total_wrote_2022),
        total_achieved_2022: String(record.total_achieved_2022),
        percent_achieved_2022: String(record.percent_achieved_2022),
        progressed_number_2023: String(record.progressed_number_2023),
        total_wrote_2023: String(record.total_wrote_2023),
        total_achieved_2023: String(record.total_achieved_2023),
        percent_achieved_2023: String(record.percent_achieved_2023),
        progressed_number_2024: String(record.progressed_number_2024),
        total_wrote_2024: String(record.total_wrote_2024),
        total_achieved_2024: String(record.total_achieved_2024),
        percent_achieved_2024: String(record.percent_achieved_2024),
        progressed_number_2025: String(record.progressed_number_2025),
        total_wrote_2025: String(record.total_wrote_2025),
        total_achieved_2025: String(record.total_achieved_2025),
        percent_achieved_2025: String(record.percent_achieved_2025),
        count: count,
      }

      count++

      await ctx.runMutation(internal.seedMutations.insertRecords, parsedRecord)
    }

    // Return success and the number of inserted records

    return { success: true, recordsInserted: records.length }
  },
})
