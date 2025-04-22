import { Button } from "~/components/ui/button";
import { Separator } from "~/components/ui/separator";
import { SidebarTrigger } from "~/components/ui/sidebar";

import { Search } from "~/components/search";
import { useTheme } from "~/context/theme-context";
import { IconCheck } from "@tabler/icons-react";
import { cn } from "~/lib/utils";

export function SiteHeader() {
  const { theme, setTheme } = useTheme();

  return (
    <header className="sticky inset-x-0 top-0 isolate z-10 flex h-(--header-height) shrink-0 items-center gap-2 pt-2 transition-[width,height] ease-linear group-has-data-[collapsible=icon]/sidebar-wrapper:h-(--header-height)">
      <div className="flex w-full items-center gap-1 px-4 lg:gap-2 lg:px-6">
        <SidebarTrigger className="-ml-1" />
        <Separator
          orientation="vertical"
          className="mx-2 data-[orientation=vertical]:h-4"
        />

        <Search />

        <Button
          onClick={() => setTheme("dark")}
          variant="ghost"
          size="sm"
          className="hidden sm:flex"
        >
          Dark
          <IconCheck
            size={14}
            className={cn("ml-auto", theme !== "dark" && "hidden")}
          />
        </Button>

        <div className="ml-auto flex items-center gap-2">
          <Button variant="ghost" asChild size="sm" className="hidden sm:flex">
            <a
              href="https://github.com/shadcn-ui/ui/tree/main/apps/v4/app/(examples)/dashboard"
              rel="noopener noreferrer"
              target="_blank"
              className="dark:text-foreground"
            >
              Help
            </a>
          </Button>
        </div>
      </div>
    </header>
  );
}
