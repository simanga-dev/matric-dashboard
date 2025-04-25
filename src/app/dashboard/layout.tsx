"use client";

import { cn } from "~/lib/utils";

import { AppSidebar } from "~/components/app-sidebar";
import { SiteHeader } from "~/components/site-header";
import { SidebarInset, SidebarProvider } from "~/components/ui/sidebar";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ThemeProvider } from "~/context/theme-context";

const queryClient = new QueryClient();

export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <ThemeProvider defaultTheme="dark" storageKey="vite-ui-theme">
      <QueryClientProvider client={queryClient}>
        <SidebarProvider
          style={
            {
              "--sidebar-width": "calc(var(--spacing) * 72)",
              "--header-height": "calc(var(--spacing) * 12)",
              "--main-height": "calc(100vh - (var(--spacing) * 16))",
            } as React.CSSProperties
          }
        >
          <AppSidebar variant="inset" />
          <SidebarInset>
            <SiteHeader />
            <div
              data-slot="sidebar-inset"
              className={cn(
                "bg-background relative m-2 h-(--main-height)",
                "md:peer-data-[variant=inset]: rounded-xl shadow-sm md:peer-data-[variant=inset]:m-2 md:peer-data-[variant=inset]:ml-0 md:peer-data-[variant=inset]:rounded-xl md:peer-data-[variant=inset]:peer-data-[state=collapsed]:ml-2",
                "sticky start-8 top-14 h-full w-full overflow-y-auto", // Added styles
              )}
            >
              {children}
            </div>
          </SidebarInset>
        </SidebarProvider>
      </QueryClientProvider>
    </ThemeProvider>
  );
}
