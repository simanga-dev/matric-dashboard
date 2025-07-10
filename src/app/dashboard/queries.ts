import { db } from "~/server/db";
import { z } from "zod";
import { sql } from "drizzle-orm";
import {
  LearnerCountSchema,
  LearnerPassRateSchema,
  SchoolCardSchema,
  SchoolCountSchema,
  SchoolSchema,
} from "~/lib/schemas";
import { school } from "~/server/db/schema";

const Q = {
  GetMatricPassRate: async function (): Promise<z.infer<
    typeof LearnerPassRateSchema
  > | null> {
    try {
      const result = await db.run(sql`
        SELECT
            ROUND(
                ((SUM(CASE WHEN year = 2023 THEN learners_pass ELSE 0 END) * 1.0) /
                 NULLIF(SUM(CASE WHEN year = 2023 THEN learners_wrote ELSE 0 END), 0)) * 100, 2
              ) AS pass_rate_2023,
            ROUND(
                ((SUM(CASE WHEN year = 2022 THEN learners_pass ELSE 0 END) * 1.0) /
                 NULLIF(SUM(CASE WHEN year = 2022 THEN learners_wrote ELSE 0 END), 0)) * 100, 2
              ) AS pass_rate_2022,
            ROUND(
                (
                    ((SUM(CASE WHEN year = 2023 THEN learners_pass ELSE 0 END) * 1.0) /
                     NULLIF(SUM(CASE WHEN year = 2023 THEN learners_wrote ELSE 0 END), 0)) -
                    ((SUM(CASE WHEN year = 2022 THEN learners_pass ELSE 0 END) * 1.0) /
                     NULLIF(SUM(CASE WHEN year = 2022 THEN learners_wrote ELSE 0 END), 0))
                ), 2
              ) AS trend_rate_2023
        FROM "matric-dashboard_marks"
        WHERE year IN (2022, 2023)
    `);

      const row = result.rows?.[0];
      const parsed = LearnerPassRateSchema.safeParse(row);

      return parsed.success ? parsed.data : null;
    } catch (error) {
      console.log(error);
      return null;
    }
  },

  GetTotalLearner: async function (): Promise<z.infer<
    typeof LearnerCountSchema
  > | null> {
    try {
      const result = await db.run(sql`
            SELECT
                total_learners_2023,
                total_learners_2022,
                CASE
                    WHEN total_learners_2022 = 0 THEN NULL
                    ELSE ROUND((total_learners_2023 - total_learners_2022) / total_learners_2023, 2)
                END AS trend_rate_learners_2023
            FROM (
                SELECT
                    SUM(CASE WHEN year = 2023 THEN learners_wrote ELSE 0 END) AS total_learners_2023,
                    SUM(CASE WHEN year = 2022 THEN learners_wrote ELSE 0 END) AS total_learners_2022
                FROM "matric-dashboard_marks"
                WHERE year IN (2022, 2023)
            ) t

`);

      const row = result.rows?.[0];
      const parsed = LearnerCountSchema.safeParse(row);
      return parsed.success ? parsed.data : null;
    } catch (error) {
      console.log(error);
      return null;
    }
  },

  GetTopSchool: async function (): Promise<z.infer<
    typeof SchoolCountSchema
  > | null> {
    try {
      const result = await db.run(sql`
        SELECT
            total_school_2023,
            total_school_2022,
            CASE
                WHEN total_school_2022 = 0 THEN NULL
                ELSE ROUND((total_school_2023 - total_school_2022) / total_school_2022, 2)
            END AS trend_rate_schools_2023
        FROM (
            SELECT
                COUNT(DISTINCT CASE WHEN year = 2023 AND learners_wrote = learners_pass THEN school_id END) AS total_school_2023,
                COUNT(DISTINCT CASE WHEN year = 2022 AND learners_wrote = learners_pass THEN school_id END) AS total_school_2022
            FROM "matric-dashboard_marks"
            WHERE year IN (2022, 2023)
        ) t

    `);

      const row = result.rows?.[0];
      const parsed = SchoolCountSchema.safeParse(row);
      return parsed.success ? parsed.data : null;
    } catch (err) {
      console.log(err);
      return null;
    }
  },

  Examcenters: async function (): Promise<z.infer<
    typeof SchoolCountSchema
  > | null> {
    try {
      const result = await db.run(sql`
        SELECT
            total_school_2023,
            total_school_2022,
            CASE
                WHEN total_school_2022 = 0 THEN NULL
                ELSE ROUND(100.0 * (total_school_2023 - total_school_2022) / total_school_2022, 2)
            END AS trend_rate_schools_2023
        FROM (
            SELECT
                COUNT(CASE WHEN year = 2023 THEN learners_wrote ELSE 0 END) AS total_school_2023,
                COUNT(CASE WHEN year = 2022 THEN learners_wrote ELSE 0 END) AS total_school_2022
            FROM "matric-dashboard_marks"
            WHERE year IN (2022, 2023)
        ) t

      `);

      const row = result.rows?.[0];
      const parsed = SchoolCountSchema.safeParse(row);
      return parsed.success ? parsed.data : null;
    } catch (error) {
      console.log(error);
      return null;
    }
  },

  GetSchools: async function (): Promise<
    z.infer<typeof SchoolSchema>[] | null
  > {
    try {
      const db_school = await db.select().from(school);
      // Use z.array(SchoolSchema) to validate and parse the db_school array
      const parsedSchools = z.array(SchoolSchema).safeParse(db_school);
      if (!parsedSchools.success) {
        console.log(parsedSchools.error);
        return null;
      }
      return parsedSchools.data;
    } catch (error) {
      console.log(error);
      return null;
    }
  },
};

export default Q;
