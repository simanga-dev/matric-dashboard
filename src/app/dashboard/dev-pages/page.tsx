"use client";
import { Button } from "~/components/ui/button";
import { create } from "../actions";

export default function Page() {
  // let data = await create()

  return (
    <div className="p-18">
      <p>Seed data from a csv files</p>
      <Button
        variant="outline"
        onClick={() => {
          console.log(create());
        }}
      >
        Seed Data
      </Button>
    </div>
  );
}
