"use client";

import React from "react";

import {
  Command,
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "./ui/command";

import { Input } from "./ui/input";

import { useQuery } from "@tanstack/react-query";
import { promises } from "dns";

interface SchoolSearchResult {
  id: number;
  province: string;
  district_name: string;
  emis_number: string;
  centre_number: string;
  centre_name: string;
  quantile: string;
  "2021_progressed_no": string; // consider changing to number if applicable
  "2021_wrote_no": string; // consider changing to number if applicable
  "2021_archived_no": string; // consider changing to number if applicable
  "2021_achived_per": string; // consider changing to number if applicable
  "2022_progressed_no": string; // consider changing to number if applicable
  "2022_wrote_no": string; // consider changing to number if applicable
  "2022_archived_no": string; // consider changing to number if applicable
  "2023_progressed_no": string; // consider changing to number if applicable
  "2023_wrote_no": string; // consider changing to number if applicable
  "2023_archived_no": string; // consider changing to number if applicable
  "2023_achived_per": string; // consider changing to number if applicable
}

// Custom hook to debounce a value
function useDebounce(value: string, delay: number) {
  const [debouncedValue, setDebouncedValue] = React.useState(value);

  React.useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedValue(value);
    }, delay);

    return () => {
      clearTimeout(handler);
    };
  }, [value, delay]);

  return debouncedValue;
}

export function Search() {
  const [open, setOpen] = React.useState(false);
  const [searchTerm, setSearchTerm] = React.useState("");

  const debouncedSearchTerm = useDebounce(searchTerm, 500);

  React.useEffect(() => {
    const down = (e: KeyboardEvent) => {
      if (e.key === "k" && (e.metaKey || e.ctrlKey)) {
        e.preventDefault();
        setOpen((open) => !open);
      }
    };
    document.addEventListener("keydown", down);
    return () => document.removeEventListener("keydown", down);
  }, []);

  const schoolQuery = useQuery({
    queryKey: ["search-school", searchTerm],
    queryFn: async (): Promises<SchoolSearchResult> => {
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

      return res.hits as SchoolSearchResult[];
    },
  });

  return (
    <>
      <div className="mx-auto flex">
        <Input
          onClick={() => setOpen((open) => !open)}
          type="search"
          placeholder="Search school"
          className="md:w-[100px] lg:w-[300px]"
        />
        <span className="right-2 my-auto block translate-x-[-120%] text-xs text-gray-500">
          control - k
        </span>
      </div>

      <CommandDialog open={open} onOpenChange={setOpen}>
        <div className="p-2">
          <Input
            onChange={(e) => setSearchTerm(e.target.value)}
            type="text"
            placeholder="Search school"
          />

          {schoolQuery.data &&
            schoolQuery.data.map((todo) => (
              <li key={todo.id}>{todo.centre_name}</li>
            ))}

          {/* { */}
          {/*   if(schoolQuery.data) */}
          {/* { */}
          {/* schoolQuery.data.map((todo) => ( */}
          {/*   <li key={todo.id}>{todo.id}</li> */}
          {/* )})} */}

          <h1>Have something amazing her</h1>
        </div>
      </CommandDialog>
    </>
  );
}
