import {
  IconTrendingDown,
  IconTrendingUp,
  IconArrowNarrowRight,
} from "@tabler/icons-react";

import { sql } from "drizzle-orm";

import { Badge } from "~/components/ui/badge";
import {
  Card,
  CardAction,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "~/components/ui/card";
import { db } from "~/server/db";

export function SectionCards() {
  return (
    <div className="*:data-[slot=card]:from-primary/5 *:data-[slot=card]:to-card dark:*:data-[slot=card]:bg-card grid grid-cols-1 gap-4 px-4 *:data-[slot=card]:bg-gradient-to-t *:data-[slot=card]:shadow-xs lg:px-6 @xl/main:grid-cols-2 @5xl/main:grid-cols-4">
      <PassRateCard />
      <LearnersCountCard />
      <ExamCentreCard />
      <TopSchoolCard />
    </div>
  );
}

async function TopSchoolCard() {
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
              COUNT(DISTINCT CASE WHEN year = 2023 AND total_wrote = total_archieved THEN school_id END) AS total_school_2023,
              COUNT(DISTINCT CASE WHEN year = 2022 AND total_wrote = total_archieved THEN school_id END) AS total_school_2022
          FROM "matric-dashboard_marks"
          WHERE year IN (2022, 2023)
      ) t

`);

  return (
    <Card className="@container/card">
      <CardHeader>
        <CardDescription>Top School Performer</CardDescription>
        <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
          {result.rows && result.rows[0]
            ? `${result.rows[0].total_school_2023}`
            : null}
        </CardTitle>
        <CardAction>
          <Badge variant="outline">
            <IconTrendingDown />
            {result.rows && result.rows[0]
              ? ` ${result.rows[0].trend_percentage_2023} %`
              : null}
          </Badge>
        </CardAction>
      </CardHeader>
      <CardFooter className="flex-col items-start gap-1.5 text-sm">
        <div className="line-clamp-1 flex gap-2 font-medium">
          100 % pass rate
        </div>
        <div className="text-muted-foreground">
          Less school archive 100% pass rate this year
        </div>
      </CardFooter>
    </Card>
  );
}

async function ExamCentreCard() {
  const result = await db.run(sql`
        SELECT
            total_student_2023,
            total_student_2022,
            trend_learners_2023,
            CASE
                WHEN total_student_2022 = 0 THEN NULL
                ELSE ROUND(100.0 * trend_learners_2023 / total_student_2022, 2)
            END AS trend_percentage_2023
        FROM (
            SELECT
                COUNT(CASE WHEN year = 2023 THEN total_wrote ELSE 0 END) AS total_student_2023,
                COUNT(CASE WHEN year = 2022 THEN total_wrote ELSE 0 END) AS total_student_2022,
                COUNT(CASE WHEN year = 2023 THEN total_wrote ELSE 0 END) -
                COUNT(CASE WHEN year = 2022 THEN total_wrote ELSE 0 END) AS trend_learners_2023
            FROM "matric-dashboard_marks"
            WHERE year IN (2022, 2023)
        ) t
`);

  return (
    <Card className="@container/card">
      <CardHeader>
        <CardDescription>Exam Centers</CardDescription>
        <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
          {result.rows && result.rows[0]
            ? `${result.rows[0].total_student_2023}`
            : null}
        </CardTitle>
        <CardAction>
          <Badge variant="outline">
            <IconArrowNarrowRight />
            {result.rows && result.rows[0]
              ? `${result.rows[0].trend_percentage_2023} %`
              : null}
          </Badge>
        </CardAction>
      </CardHeader>
      <CardFooter className="flex-col items-start gap-1.5 text-sm">
        <div className="line-clamp-1 flex gap-2 font-medium">
          Constant Exam Centre <IconArrowNarrowRight className="size-4" />
        </div>
        <div className="text-muted-foreground">
          The Number of Exam Centres Remains The Same
        </div>
      </CardFooter>
    </Card>
  );
}

async function LearnersCountCard() {
  const result = await db.run(sql`
        SELECT
            total_student_2023,
            total_student_2022,
            trend_learners_2023,
            CASE
                WHEN total_student_2022 = 0 THEN NULL
                ELSE ROUND(100.0 * trend_learners_2023 / total_student_2022, 2)
            END AS trend_percentage_2023
        FROM (
            SELECT
                SUM(CASE WHEN year = 2023 THEN total_wrote ELSE 0 END) AS total_student_2023,
                SUM(CASE WHEN year = 2022 THEN total_wrote ELSE 0 END) AS total_student_2022,
                SUM(CASE WHEN year = 2023 THEN total_wrote ELSE 0 END) -
                SUM(CASE WHEN year = 2022 THEN total_wrote ELSE 0 END) AS trend_learners_2023
            FROM "matric-dashboard_marks"
            WHERE year IN (2022, 2023)
        ) t
`);

  return (
    <Card className="@container/card">
      <CardHeader>
        <CardDescription>Learners in 2023</CardDescription>
        <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
          {result.rows && result.rows[0]
            ? `${result.rows[0].total_student_2023}`
            : null}
        </CardTitle>
        <CardAction>
          <Badge variant="outline">
            <IconTrendingDown />
            {result.rows && result.rows[0]
              ? `${result.rows[0].trend_percentage_2023} %`
              : null}
          </Badge>
        </CardAction>
      </CardHeader>
      <CardFooter className="flex-col items-start gap-1.5 text-sm">
        <div className="line-clamp-1 flex gap-2 font-medium">
          Learners decreased by
          {result.rows && result.rows[0]
            ? ` ${result.rows[0].trend_learners_2023}`
            : null}
          <IconTrendingDown className="size-4" />
        </div>
        <div className="text-muted-foreground">
          Few students wrote matric this year than last year
        </div>
      </CardFooter>
    </Card>
  );
}

async function PassRateCard() {
  const result = await db.run(sql`
        SELECT
            ROUND(
                ((SUM(CASE WHEN year = 2023 THEN total_archieved ELSE 0 END) * 1.0) /
                 NULLIF(SUM(CASE WHEN year = 2023 THEN total_wrote ELSE 0 END), 0)) * 100, 2
              ) AS pass_rate_2023,
            ROUND(
                ((SUM(CASE WHEN year = 2022 THEN total_archieved ELSE 0 END) * 1.0) /
                 NULLIF(SUM(CASE WHEN year = 2022 THEN total_wrote ELSE 0 END), 0)) * 100, 2
              ) AS pass_rate_2022,
            ROUND(
                (
                    ((SUM(CASE WHEN year = 2023 THEN total_archieved ELSE 0 END) * 1.0) /
                     NULLIF(SUM(CASE WHEN year = 2023 THEN total_wrote ELSE 0 END), 0)) -
                    ((SUM(CASE WHEN year = 2022 THEN total_archieved ELSE 0 END) * 1.0) /
                     NULLIF(SUM(CASE WHEN year = 2022 THEN total_wrote ELSE 0 END), 0))
                ) * 100, 2
              ) AS trend_percentage_points
        FROM "matric-dashboard_marks"
        WHERE year IN (2022, 2023)
`);

  return (
    <Card className="@container/card">
      <CardHeader>
        <CardDescription>Pass Rate 2023</CardDescription>
        <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
          {result.rows && result.rows[0]
            ? `${result.rows[0].pass_rate_2023} %`
            : null}
        </CardTitle>
        <CardAction>
          <Badge variant="outline">
            <IconTrendingUp />
            {result.rows && result.rows[0]
              ? `+${result.rows[0].trend_percentage_points} %`
              : null}
          </Badge>
        </CardAction>
      </CardHeader>
      <CardFooter className="flex-col items-start gap-1.5 text-sm">
        <div className="line-clamp-1 flex gap-2 font-medium">
          Trending up this year <IconTrendingUp className="size-4" />
        </div>
        <div className="text-muted-foreground">
          Learnes who pass their Matric Results in 2023
        </div>
      </CardFooter>
    </Card>
  );
}
