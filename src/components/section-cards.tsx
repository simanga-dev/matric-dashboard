import {
  IconTrendingDown,
  IconTrendingUp,
  IconArrowNarrowRight,
} from "@tabler/icons-react";

import Q from "~/app/dashboard/queries";

import { Badge } from "~/components/ui/badge";
import {
  Card,
  CardAction,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "~/components/ui/card";

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
  const r = await Q.GetTopSchool();
  if (r == null) return <Card className="@container/card"></Card>;

  return (
    <Card className="@container/card">
      <CardHeader>
        <CardDescription>Top School Performer</CardDescription>
        <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
          {r.total_school_2023}
        </CardTitle>
        <CardAction>
          <Badge variant="outline">
            <IconTrendingDown />
            {r.trend_percentage_2023} %
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
  const r = await Q.Examcenters();
  if (r == null) return <Card className="@container/card"></Card>;

  return (
    <Card className="@container/card">
      <CardHeader>
        <CardDescription>Exam Centers</CardDescription>
        <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
          {r.total_school_2022}
        </CardTitle>
        <CardAction>
          <Badge variant="outline">
            <IconArrowNarrowRight />
            {r.trend_percentage_2023}
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
  const r = await Q.GetTotalLearner();

  if (r == null) return <Card className="@container/card"></Card>;

  return (
    <Card className="@container/card">
      <CardHeader>
        <CardDescription>Learners in 2023</CardDescription>
        <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
          {r.total_learners_2023}
        </CardTitle>
        <CardAction>
          <Badge variant="outline">
            <IconTrendingDown />
            {r.trend_percentage_2023}
          </Badge>
        </CardAction>
      </CardHeader>
      <CardFooter className="flex-col items-start gap-1.5 text-sm">
        <div className="line-clamp-1 flex gap-2 font-medium">
          Learners decreased by
          {r.trend_learners_2023}
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
  const r = await Q.GetMatricPassRate();
  if (r == null) return <Card className="@container/card"></Card>;
  return (
    <Card className="@container/card">
      <CardHeader>
        <CardDescription>Pass Rate 2023</CardDescription>
        <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
          {r.total_learners_2023} %
        </CardTitle>
        <CardAction>
          <Badge variant="outline">
            <IconTrendingUp />
            {r.trend_percentage_2023} %`
          </Badge>
        </CardAction>
      </CardHeader>
      <CardFooter className="flex-col items-start gap-1.5 text-sm">
        <div className="line-clamp-1 flex gap-2 font-medium">
          Trending up this year <IconTrendingUp className="size-4" />
        </div>
        <div className="text-muted-foreground">
          Learners who pass their Matric results in 2023
        </div>
      </CardFooter>
    </Card>
  );
}
