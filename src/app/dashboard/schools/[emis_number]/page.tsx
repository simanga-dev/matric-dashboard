import { db } from "~/server/db";

import { eq } from "drizzle-orm";
import { school } from "~/server/db/schema";

export default async function Page({
  params,
}: {
  params: Promise<{ emis_number: string }>;
}) {
  const { emis_number } = await params;

  const db_school = await db
    .select()
    .from(school)
    .where(eq(school.natemis, parseInt(emis_number)))
    .limit(1);

  if (db_school != null) {
    return (
      <div className="p-20">
        <h2 className="text-2xl font-bold tracking-tight">
          {db_school[0]?.official_institution_name}
        </h2>
        <p className="text-muted-foreground">
          Province: {db_school[0]?.province}
        </p>

        <p className="text-muted-foreground">
          Amazing: {db_school[0]?.quantile}
        </p>
        <p></p>
      </div>
    );
  }

  return <h1> Try other school.... we seem not to find the school</h1>;
}
