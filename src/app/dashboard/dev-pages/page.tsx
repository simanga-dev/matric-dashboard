"use client";
import { Button } from "~/components/ui/button";
import { seedData2012, seedData2023,seedData2024 } from "../actions";

export default function Page() {
  return (
    <>
      <div className="p-18">
        <p>seed 2022 - 2024 Data</p>
        <Button variant="outline" onClick={() => seedData2024()}>
          Seed Data
        </Button>
      </div>

      <div className="p-18">
        <p>seed 2021 - 2023 Data</p>
        <Button variant="outline" onClick={() => seedData2023()}>
          Seed Data
        </Button>
      </div>

      <div className="p-18">
        <p>Seed 2010 - 2012 Data</p>
        <Button variant="outline" onClick={() => seedData2012()}>
          Seed Data
        </Button>
      </div>
    </>
  );
}
