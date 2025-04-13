"use server";

import { parse } from "csv-parse";
import fs from "node:fs";
import path from "path";
import { db } from "~/server/db";
import { school } from "~/server/db/schema";

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
      const insertedSchool = await db
        .insert(school)
        .values({
          natemis: record.emis_number,
          quantile: record.quantile,
          centre_number: record.quantile,
          province: record.province.replace(/'/g, "''"),
          official_institution_name: record.centre_name.replace(/'/g, "''"),
          district_name: record.district_name.replace(/'/g, "''"),
        })
        .returning();
      console.log("Inserted school:", insertedSchool);
    }
  } catch (error) {
    console.error("Error processing CSV files:", error);
  }
}
