"use server";

import { parse } from "csv-parse";
import fs from "node:fs";
import path from "path";
import { db } from "~/server/db";
import { school, marks } from "~/server/db/schema";
import { eq, lt, gte, ne, sql } from "drizzle-orm";

export async function create() {
  try {
    // const filePath = path.join("/home/simanga/Workspace/2012data/117-file.txt");
    const filePath = path.join(
      "/home/simanga/layground/matric-dashboard-data-proccess/og-data/school_report_2021_2023.csv",
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

        `,
        )
        .limit(1);

      let schoolRecord = find[0];
      //
      if (schoolRecord) {
        // await db.update(marks).set({
        //   school_id: schoolRecord.id,
        //   year: 2012,
        //   learners_wrote: record["2012_wrote_number"],
        //   learners_pass: record["2012_achieved_number"],
        //   centre_number: record["centre_number"],
        //   quantile: record["quantile"],
        //   dinaledi: record["dinaledi"],
        // }).where({year: }) ;

        //   const [insertedSchool] = await db
        //     .insert(school)
        //     .values({
        //       natemis: record.emis_number,
        //       province: record.province,
        //       official_institution_name: record.centre_name,
        //       district_name: record.district_name,
        //     })
        //     .returning();
        //
        //   schoolRecord = insertedSchool;
        // }

        // if (schoolRecord) {
        // await db.insert(marks).values({
        //   school_id: schoolRecord.id,
        //   year: 2010,
        //   learners_wrote: record["2010_wrote_number"],
        //   learners_pass: record["2010_achieved_number"],
        //   centre_number: record["centre_number"],
        //   quantile: record["quantile"],
        //   dinaledi: record["dinaledi"],
        // });
        //
        // await db.insert(marks).values({
        //   school_id: schoolRecord.id,
        //   year: 2011,
        //   learners_wrote: record["2011_wrote_number"],
        //   learners_pass: record["2011_achieved_number"],
        //   centre_number: record["centre_number"],
        //   quantile: record["quantile"],
        //   dinaledi: record["dinaledi"],
        // });
        //
        // await db.insert(marks).values({
        //   school_id: schoolRecord.id,
        //   year: 2012,
        //   learners_wrote: record["2012_wrote_number"],
        //   learners_pass: record["2012_achieved_number"],
        //   centre_number: record["centre_number"],
        //   quantile: record["quantile"],
        //   dinaledi: record["dinaledi"],
        // });

        console.log(record);
      }
    }
  } catch (error) {
    console.error("Error processing CSV files:", error);
  }
}
