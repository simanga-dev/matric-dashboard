import { ChartAreaInteractive } from "~/components/chart-area-interactive";
import { DataTable } from "~/components/data-table";
import { SectionCards } from "~/components/section-cards";

export const dynamic = "force-dynamic";

import Q from "./queries";
import { Card } from "~/components/ui/card";

export default async function Page() {
  return (
    <div className="flex flex-1 flex-col">
      <div className="@container/main flex flex-1 flex-col gap-2">
        <div className="flex flex-col gap-4 py-4 md:gap-6 md:py-6">
          <SectionCards />
          <div className="px-4 lg:px-6">
            <ChartAreaInteractiveData />
          </div>
          {/* <DataTable data={school_data} /> */}
        </div>
      </div>
    </div>
  );
}

const ChartAreaInteractiveData = async () => {
  const r = await Q.GetPassRateChart();

  console.log(r);

  if (r == null) return <Card className="@container/card"></Card>;

  return <ChartAreaInteractive data={r} />;
};
