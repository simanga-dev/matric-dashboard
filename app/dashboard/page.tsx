"use client";

import { AppSidebar } from "@/components/app-sidebar";
import { Search } from "@/components/search";
import {
    useQueryClient,
    QueryClient,
    QueryClientProvider,
} from '@tanstack/react-query'


import { Separator } from "@/components/ui/separator";
import {
    SidebarInset,
    SidebarProvider,
    SidebarTrigger,
} from "@/components/ui/sidebar";
import { UserNav } from "@/components/user-nav";

const queryClient = new QueryClient()


export default function Page() {
    return (
        <QueryClientProvider client={queryClient}>
            <SidebarProvider>
                <AppSidebar />
                <SidebarInset>
                    <header className="flex justify-center h-16 shrink-0 items-center gap-2 border-b px-4">
                        <div className="mx-auto">
                            <Search />
                        </div>

                        <div className="">
                            <UserNav />
                        </div>

                    </header>

                    <div className="flex flex-1 flex-col gap-4 p-4">
                        <div className="grid auto-rows-min gap-4 md:grid-cols-3">
                            <div className="aspect-video rounded-xl bg-muted/50" />
                            <div className="aspect-video rounded-xl bg-muted/50" />
                            <div className="aspect-video rounded-xl bg-muted/50" />
                        </div>
                        <div className="min-h-[100vh] flex-1 rounded-xl bg-muted/50 md:min-h-min" />
                    </div>
                </SidebarInset>
            </SidebarProvider>
        </QueryClientProvider>

    );
}
