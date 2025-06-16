"use server";

import { parse } from "csv-parse";
import fs from "node:fs";
import path from "path";
import { db } from "~/server/db";
import { school, marks } from "~/server/db/schema";

export async function create() {
  try {
    const filePath = path.join(
      "/home/simanga/Playground/matric-dashboard-data-proccess/og-data/school_report_2021_2023.csv",
    );

    const parser = fs.createReadStream(filePath).pipe(
      parse({
        // CSV options if any
        columns: true,
        skip_empty_lines: true,
      }),
    );

    for await (const record of parser) {
      const [insertedSchool] = await db
        .insert(school)
        .values({
          natemis: record.emis_number,
          quantile: record.quantile,
          centre_number: record.centre_number,
          province: record.province.replace(/'/g, "''"),
          official_institution_name: record.centre_name.replace(/'/g, "''"),
          district_name: record.district_name.replace(/'/g, "''"),
        })
        .returning();

      if (insertedSchool) {
        await db.insert(marks).values({
          school_id: insertedSchool.id,
          year: 2021,
          number_progressed: record[" 2021_progressed_no"],
          total_wrote: record["2021_wrote_no"],
          total_archived: record["2021_archived_no"], // use correct field name
          percentage_archived: record["2021_achived_per"],
        });

        await db.insert(marks).values({
          school_id: insertedSchool.id,
          year: 2022,
          number_progressed: record[" 2022_progressed_no"],
          total_wrote: record["2022_wrote_no"],
          total_archived: record["2022_archived_no"], // use correct field name
          percentage_archived: 0,
        });

        await db.insert(marks).values({
          school_id: insertedSchool.id,
          year: 2023,
          number_progressed: record[" 2023_progressed_no"],
          total_wrote: record["2023_wrote_no"],
          total_archived: record["2023_archived_no"], // use correct field name
          percentage_archived: record["2023_achived_per"],
        });
      }

      console.log(record);
    }
  } catch (error) {
    console.error("Error processing CSV files:", error);
  }
}
