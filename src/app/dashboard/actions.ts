"use server";

import { parse } from "csv-parse";
import { sql } from "drizzle-orm";
import fs from "node:fs";
import path from "path";
import { db } from "~/server/db";
import { school, marks } from "~/server/db/schema";

export async function seedData2023() {
  try {
    const filePath = path.join(
      "/home/simanga/layground/matric-dashboard-data-proccess/og-data/school_report_2021_2023.csv",
    );

    const parser = fs.createReadStream(filePath).pipe(
      parse({
        skip_empty_lines: true,
        columns: true,
        trim: true,
      }),
    );

    for await (const record of parser) {
      var find = await db
        .select()
        .from(school)
        .where(
          sql`lower(${school.natemis}) = lower(${record.emis_number}) and
            lower(${school.province}) = lower(${record.province}) and
            lower(${school.official_institution_name}) = lower(${record.centre_name})
        `,
        )
        .limit(1);

      let schoolRecord = find[0];

      if (!schoolRecord) {
        const [insertedSchool] = await db
          .insert(school)
          .values({
            natemis: record.emis_number,
            province: record.province.replace(/'/g, "''"),
            official_institution_name: record.centre_name.replace(/'/g, "''"),
            district_name: record.district_name.replace(/'/g, "''"),
          })
          .returning();

        schoolRecord = insertedSchool;
      }

      if (schoolRecord) {
        await db.insert(marks).values({
          year: 2021,
          school_id: schoolRecord.id,
          dinaledi: record["dinaledi"],
          quantile: record.quantile,
          centre_number: record.centre_number,
          learners_wrote: record["2021_wrote_no"],
          learners_pass: record["2021_archived_no"],
        });

        await db.insert(marks).values({
          year: 2022,
          school_id: schoolRecord.id,
          dinaledi: record["dinaledi"],
          quantile: record.quantile,
          centre_number: record.centre_number,
          learners_wrote: record["2022_wrote_no"],
          learners_pass: record["2021_archived_no"],
        });

        await db.insert(marks).values({
          year: 2023,
          school_id: schoolRecord.id,
          dinaledi: record["dinaledi"],
          quantile: record.quantile,
          centre_number: record.centre_number,
          learners_wrote: record["2023_wrote_no"],
          learners_pass: record["2023_archived_no"],
        });

        console.log(record);
      }
    }
  } catch (error) {
    console.error("Error processing CSV files:", error);
  }
}

export async function seedData2012() {
  try {
    const filePath = path.join("/home/simanga/Workspace/2012data/117-file.txt");

    const parser = fs.createReadStream(filePath).pipe(
      parse({
        // CSV options if any
        columns: true,
        skip_empty_lines: true,
        to_line: 911,
        trim: true,
      }),
    );

    for await (const record of parser) {
      var find = await db
        .select()
        .from(school)
        .where(
          sql`lower(${school.natemis}) = lower(${record.emis_number}) and
            lower(${school.province}) = lower(${record.province}) and
            lower(${school.official_institution_name}) = lower(${record.centre_name})
        `,
        )
        .limit(1);

      let schoolRecord = find[0];

      if (!schoolRecord) {
        const [insertedSchool] = await db
          .insert(school)
          .values({
            natemis: record.emis_number,
            province: record.province.replace(/'/g, "''"),
            official_institution_name: record.centre_name.replace(/'/g, "''"),
            district_name: record.district_name.replace(/'/g, "''"),
          })
          .returning();

        schoolRecord = insertedSchool;
      }

      if (schoolRecord) {
        await db.insert(marks).values({
          year: 2010,
          school_id: schoolRecord.id,
          dinaledi: record["dinaledi"],
          quantile: record.quantile,
          centre_number: record.centre_number,
          learners_wrote: record["2010_wrote_number"],
          learners_pass: record["2010_achieved_number"],
        });

        await db.insert(marks).values({
          year: 2011,
          school_id: schoolRecord.id,
          dinaledi: record["dinaledi"],
          quantile: record.quantile,
          centre_number: record.centre_number,
          learners_wrote: record["2011_wrote_number"],
          learners_pass: record["2011_achieved_number"],
        });

        await db.insert(marks).values({
          year: 2012,
          school_id: schoolRecord.id,
          dinaledi: record["dinaledi"],
          quantile: record.quantile,
          centre_number: record.centre_number,
          learners_wrote: record["2012_wrote_number"],
          learners_pass: record["2012_achieved_number"],
        });

        console.log(record);
      }
    }
  } catch (error) {
    console.error("Error processing CSV files:", error);
  }
}

export async function seedData2024() {
  try {
    const filePath = path.join(
      "/home/simanga/Playground/2024-data-clean-up/2024_nsc_school_performance_report.csv",
    );

    const parser = fs.createReadStream(filePath).pipe(
      parse({
        // CSV options if any
        columns: true,
        skip_empty_lines: true,
        // to_line: 911,
        trim: true,
      }),
    );

    for await (const record of parser) {
      var find = await db
        .select()
        .from(school)
        .where(
          sql`lower(${school.natemis}) = lower(${record.emis_number}) and
            lower(${school.province}) = lower(${record.province}) and
            lower(${school.official_institution_name}) = lower(${record.centre_name})
        `,
        )
        .limit(1);

      let schoolRecord = find[0];

      if (!schoolRecord) {
        const [insertedSchool] = await db
          .insert(school)
          .values({
            natemis: record.emis_number,
            province: record.province.replace(/'/g, "''"),
            official_institution_name: record.centre_name.replace(/'/g, "''"),
            district_name: record.district_name.replace(/'/g, "''"),
          })
          .returning();

        schoolRecord = insertedSchool;

      }

      if (schoolRecord) {
        await db.insert(marks).values({
          year: 2022,
          school_id: schoolRecord.id,
          dinaledi: record["dinaledi"],
          quantile: record.quantile,
          centre_number: record.centre_number,
          progressed_number: record.progressed_number_2022,
          learners_wrote: record.total_wrote_2022,
          learners_pass: record.total_achieved_2022,
        });

        await db.insert(marks).values({
          year: 2023,
          school_id: schoolRecord.id,
          dinaledi: record["dinaledi"],
          quantile: record.quantile,
          centre_number: record.centre_number,
          progressed_number: record.progressed_number_2023,
          learners_wrote: record.total_wrote_2023,
          learners_pass: record.total_achieved_2023,
        });

        await db.insert(marks).values({
          year: 2024,
          school_id: schoolRecord.id,
          dinaledi: record["dinaledi"],
          quantile: record.quantile,
          centre_number: record.centre_number,
          progressed_number: record.progressed_number_2024,
          learners_wrote: record.total_wrote_2024,
          learners_pass: record.total_achieved_2024,
        });

        console.log(record);
      }
    }
  } catch (error) {
    console.error("Error processing CSV files:", error);
  }
}
