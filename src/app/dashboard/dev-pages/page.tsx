"use client";
import { Button } from "~/components/ui/button";
import { create } from "../actions";

export default function Page() {
  return (
    <div className="p-18">
      <p>Seed data from a csv files</p>
      <Button variant="outline" onClick={() => create()}>
        Seed Data
      </Button>
    </div>
  );
}
