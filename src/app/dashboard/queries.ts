import { db } from "~/server/db";
import { z } from "zod";
import { sql } from "drizzle-orm";

const SchoolCardSchema = z.object({
  total_school_2023: z.number(),
  total_school_2022: z.number(),
  trend_schools_2023: z.number(),
  trend_percentage_2023: z.number().nullable(),
});

const LearnerCardSchema = z.object({
  total_learners_2023: z.number(),
  total_learners_2022: z.number(),
  trend_learners_2023: z.number(),
  trend_percentage_2023: z.number().nullable(),
});

type SchoolTrends = z.infer<typeof SchoolCardSchema>;
type LearnerTrends = z.infer<typeof LearnerCardSchema>;

const Q = {
  GetMatricPassRate: async function (): Promise<LearnerTrends | null> {
    try {
      const result = await db.run(sql`
        SELECT
            ROUND(
                ((SUM(CASE WHEN year = 2023 THEN total_archived ELSE 0 END) * 1.0) /
                 NULLIF(SUM(CASE WHEN year = 2023 THEN total_wrote ELSE 0 END), 0)) * 100, 2
              ) AS total_learners_2023,
            ROUND(
                ((SUM(CASE WHEN year = 2022 THEN total_archived ELSE 0 END) * 1.0) /
                 NULLIF(SUM(CASE WHEN year = 2022 THEN total_wrote ELSE 0 END), 0)) * 100, 2
              ) AS total_learners_2022,
            ROUND(
                (
                    ((SUM(CASE WHEN year = 2023 THEN total_archived ELSE 0 END) * 1.0) /
                     NULLIF(SUM(CASE WHEN year = 2023 THEN total_wrote ELSE 0 END), 0)) -
                    ((SUM(CASE WHEN year = 2022 THEN total_archived ELSE 0 END) * 1.0) /
                     NULLIF(SUM(CASE WHEN year = 2022 THEN total_wrote ELSE 0 END), 0))
                ), 2
              ) AS trend_learners_2023,

            ROUND(
                (
                    ((SUM(CASE WHEN year = 2023 THEN total_archived ELSE 0 END) * 1.0) /
                     NULLIF(SUM(CASE WHEN year = 2023 THEN total_wrote ELSE 0 END), 0)) -
                    ((SUM(CASE WHEN year = 2022 THEN total_archived ELSE 0 END) * 1.0) /
                     NULLIF(SUM(CASE WHEN year = 2022 THEN total_wrote ELSE 0 END), 0))
                ) * 100, 2
              ) AS trend_percentage_2023

        FROM "matric-dashboard_marks"
        WHERE year IN (2022, 2023)
    `);

      const row = result.rows?.[0];
      const parsed = LearnerCardSchema.safeParse(row);

      return parsed.success ? parsed.data : null;
    } catch (error) {
      console.log(error);
      return null;
    }
  },

  GetTotalLearner: async function (): Promise<LearnerTrends | null> {
    try {
      const result = await db.run(sql`
        SELECT
            total_learners_2023,
            total_learners_2022,
            trend_learners_2023,
            CASE
                WHEN total_learners_2022 = 0 THEN NULL
                ELSE ROUND(100.0 * trend_learners_2023 / total_learners_2022, 2)
            END AS trend_percentage_2023
        FROM (
            SELECT
                SUM(CASE WHEN year = 2023 THEN total_wrote ELSE 0 END) AS total_learners_2023,
                SUM(CASE WHEN year = 2022 THEN total_wrote ELSE 0 END) AS total_learners_2022,
                SUM(CASE WHEN year = 2023 THEN total_wrote ELSE 0 END) -
                SUM(CASE WHEN year = 2022 THEN total_wrote ELSE 0 END) AS trend_learners_2023
            FROM "matric-dashboard_marks"
            WHERE year IN (2022, 2023)
        ) t
`);

      const row = result.rows?.[0];
      const parsed = LearnerCardSchema.safeParse(row);
      return parsed.success ? parsed.data : null;
    } catch (error) {
      console.log(error);
      return null;
    }
  },

  GetTopSchool: async function (): Promise<SchoolTrends | null> {
    try {
      const result = await db.run(sql`
      SELECT
          total_school_2023,
          total_school_2022,
          total_school_2023 - total_school_2022 AS trend_schools_2023,
          CASE
              WHEN total_school_2022 = 0 THEN NULL
              ELSE ROUND(100.0 * (total_school_2023 - total_school_2022) / total_school_2022, 2)
          END AS trend_percentage_2023
      FROM (
          SELECT
              COUNT(DISTINCT CASE WHEN year = 2023 AND total_wrote = total_archived THEN school_id END) AS total_school_2023,
              COUNT(DISTINCT CASE WHEN year = 2022 AND total_wrote = total_archived THEN school_id END) AS total_school_2022
          FROM "matric-dashboard_marks"
          WHERE year IN (2022, 2023)
      ) t
    `);

      const row = result.rows?.[0];
      const parsed = SchoolCardSchema.safeParse(row);
      return parsed.success ? parsed.data : null;
    } catch (err) {
      console.log(err);
      return null;
    }
  },

  Examcenters: async function (): Promise<SchoolTrends | null> {
    try {
      const result = await db.run(sql`
        SELECT
            total_school_2023,
            total_school_2022,
            total_school_2023 - total_school_2022 AS trend_schools_2023,
            CASE
                WHEN total_school_2022 = 0 THEN NULL
                ELSE ROUND(100.0 * total_school_2023 / total_school_2022, 2)
            END AS trend_percentage_2023
        FROM (
            SELECT
                COUNT(CASE WHEN year = 2023 THEN total_wrote ELSE 0 END) AS total_school_2023,
                COUNT(CASE WHEN year = 2022 THEN total_wrote ELSE 0 END) AS total_school_2022,
                COUNT(CASE WHEN year = 2023 THEN total_wrote ELSE 0 END) -
                COUNT(CASE WHEN year = 2022 THEN total_wrote ELSE 0 END) AS trend_school_2023
            FROM "matric-dashboard_marks"
            WHERE year IN (2022, 2023)
        ) t
      `);

      const row = result.rows?.[0];
      const parsed = SchoolCardSchema.safeParse(row);
      return parsed.success ? parsed.data : null;
    } catch (error) {
      console.log(error);
      return null;
    }
  },
};

export default Q;
