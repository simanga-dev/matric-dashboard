"use client";

import React from "react";

import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "./ui/command";

import Link from "next/link";
import { IconSchool } from "@tabler/icons-react";

import { useQuery } from "@tanstack/react-query";

interface SchoolSearchResult {
  id: number;
  province: string;
  district_name: string;
  emis_number: string;
  centre_number: string;
  centre_name: string;
  quantile: string;
  "2021_progressed_no": number;
  "2021_wrote_no": number;
  "2021_archived_no": number;
  "2021_achived_per": number;
  "2022_progressed_no": number;
  "2022_wrote_no": number;
  "2022_archived_no": number;
  "2023_progressed_no": number;
  "2023_wrote_no": number;
  "2023_archived_no": number;
  "2023_achived_per": number;
}

const transformToSchoolSearchResult = (hit: any): SchoolSearchResult => ({
  id: hit.id ?? 0,
  province: hit.province || "",
  district_name: hit.district_name || "",
  emis_number: hit.emis_number || "",
  centre_number: hit.centre_number || "",
  centre_name: hit.centre_name || "",
  quantile: hit.quantile || "",
  "2021_progressed_no": Number(hit["2021_progressed_no"]) || 0,
  "2021_wrote_no": Number(hit["2021_wrote_no"]) || 0,
  "2021_archived_no": Number(hit["2021_archived_no"]) || 0,
  "2021_achived_per": Number(hit["2021_achived_per"]) || 0,
  "2022_progressed_no": Number(hit["2022_progressed_no"]) || 0,
  "2022_wrote_no": Number(hit["2022_wrote_no"]) || 0,
  "2022_archived_no": Number(hit["2022_archived_no"]) || 0,
  "2023_progressed_no": Number(hit["2023_progressed_no"]) || 0,
  "2023_wrote_no": Number(hit["2023_wrote_no"]) || 0,
  "2023_archived_no": Number(hit["2023_archived_no"]) || 0,
  "2023_achived_per": Number(hit["2023_achived_per"]) || 0,
});

// Custom hook to debounce a value

export function Search() {
  const [open, setOpen] = React.useState(false);
  const [searchTerm, setSearchTerm] = React.useState("");

  React.useEffect(() => {
    const down = (e: KeyboardEvent) => {
      if (e.key === "/" && e.ctrlKey) {
        e.preventDefault();
        setOpen((open) => !open);
      } else if (e.key === "Escape") {
        setOpen(false);
      }
    };
    document.addEventListener("keydown", down);
    return () => document.removeEventListener("keydown", down);
  }, []);

  const schoolQuery = useQuery({
    queryKey: ["search-school", searchTerm],
    queryFn: async (): Promise<SchoolSearchResult[] | null> => {
      // if (searchTerm === '') return [] // Avoid making the request if the searchTerm is empty
      const url = "http://localhost:7700/indexes/schools/search";
      const response = await fetch(url, {
        method: "POST",
        headers: {
          Authorization: `Bearer Oci4upBIoWH3PCCXdHKQ6KisJtuDrq_1gUc6u8IvXJ8`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ q: searchTerm }),
      });

      const res = await response.json();

      const results: SchoolSearchResult[] = res.hits.map(
        transformToSchoolSearchResult,
      );

      return results;
    },
  });

  return (
    <div className="relative mx-auto inline-flex">
      <Command shouldFilter={false} className="bg-border border-b-white">
        <CommandInput
          className="bg-border h-8 w-fit min-w-80 border-b border-transparent ps-9 pe-9"
          placeholder="Type a command or search..."
          onClick={() => setOpen((open) => !open)}
          onChangeCapture={(e) =>
            setSearchTerm((e.target as HTMLInputElement).value)
          }
        />
        <div className="text-muted-foreground pointer-events-none absolute inset-y-0 end-0 flex items-center justify-center pe-2">
          <kbd className="bg-background text-muted-foreground/70 inline-flex size-5 max-h-full items-center justify-center rounded px-1 font-[inherit] text-[0.625rem] font-medium shadow-xs">
            /
          </kbd>
        </div>

        <CommandList
          className={`bg-border absolute start-0 end-0 top-10 rounded-lg ${open ? "" : "hidden"}`}
          onBlur={() => setOpen(false)}
        >
          <CommandEmpty>No results found.</CommandEmpty>
          <CommandGroup heading="Search School">
            {schoolQuery.data?.map((todo) => (
              <CommandItem key={todo.id}>
                <IconSchool />
                <Link
                  onClick={() => setOpen(false)}
                  href={`/dashboard/schools/${todo.emis_number}`}
                >
                  {todo.centre_name}
                </Link>
              </CommandItem>
            ))}
          </CommandGroup>
        </CommandList>
      </Command>
    </div>
  );
}
