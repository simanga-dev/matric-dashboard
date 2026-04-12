import * as React from 'react'
import {
  IconCamera,
  IconChartBar,
  IconDashboard,
  IconDatabase,
  IconFileAi,
  IconFileDescription,
  IconFilePencil,
  IconFileWord,
  IconHelp,
  IconListDetails,
  IconReport,
  IconSearch,
  IconSettings,
  IconTrophy,
} from '@tabler/icons-react'
import { GalleryVerticalEnd } from 'lucide-react'
import { Link } from '@tanstack/react-router'

import { NavDocuments } from '~/components/nav-documents'
import { NavMain } from '~/components/nav-main'
import { NavDevelopment } from '~/components/nav-developmet'
import { NavUser } from '~/components/nav-user'
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
  SidebarSeparator,
} from '~/components/ui/sidebar'

const data = {
  navMain: [
    {
      title: 'Dashboard',
      url: '/dashboard',
      icon: IconDashboard,
    },
    {
      title: 'Analytic',
      url: '/dashboard/analytic',
      icon: IconChartBar,
    },
    {
      title: 'Schools',
      url: '/dashboard/schools',
      icon: IconListDetails,
    },
    {
      title: 'Top Achievers',
      url: '/dashboard/top-achievers',
      icon: IconTrophy,
    },
  ],
  navClouds: [
    {
      title: 'Capture',
      icon: IconCamera,
      isActive: true,
      url: '#',
      items: [
        {
          title: 'Active Proposals',
          url: '#',
        },
        {
          title: 'Archived',
          url: '#',
        },
      ],
    },
    {
      title: 'Proposal',
      icon: IconFileDescription,
      url: '#',
      items: [
        {
          title: 'Active Proposals',
          url: '#',
        },
        {
          title: 'Archived',
          url: '#',
        },
      ],
    },
    {
      title: 'Prompts',
      icon: IconFileAi,
      url: '#',
      items: [
        {
          title: 'Active Proposals',
          url: '#',
        },
        {
          title: 'Archived',
          url: '#',
        },
      ],
    },
  ],
  documents: [
    {
      name: 'Past Papers',
      url: '/dashboard/past-papers',
      icon: IconFilePencil,
    },
    {
      name: 'Study Guide',
      url: '/dashboard/study-guide',
      icon: IconFileDescription,
    },
    {
      name: 'Data Library',
      url: '#',
      icon: IconDatabase,
    },
    {
      name: 'Reports',
      url: '#',
      icon: IconReport,
    },
    {
      name: 'Word Assistant',
      url: '#',
      icon: IconFileWord,
    },
  ],

  navDevelopmet: [
    {
      title: 'Seed Data',
      url: '/dashboard/seed-data',
      icon: IconSettings,
    },
    {
      title: 'check API',
      url: '#',
      icon: IconHelp,
    },
    {
      title: 'SQL Version',
      url: '#',
      icon: IconSearch,
    },
  ],
}

export function AppSidebar({ ...props }: React.ComponentProps<typeof Sidebar>) {
  const isDevelopment = process.env.NODE_ENV === 'development'

  return (
    <Sidebar collapsible="icon" {...props}>
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton tooltip="Matric Dashboard" asChild size="lg">
              <Link to="/" preload="render">
                <div className="flex size-8 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground">
                  <GalleryVerticalEnd className="size-4" />
                </div>
                <div className="grid flex-1 text-left text-sm leading-tight">
                  <span className="truncate font-semibold">
                    Matric Dashboard
                  </span>
                  <span className="truncate text-xs">School analytics</span>
                </div>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>
      <SidebarContent>
        <NavMain items={data.navMain} />
        <NavDocuments items={data.documents} />

        <SidebarSeparator />

        {isDevelopment && <NavDevelopment items={data.navDevelopmet} />}
      </SidebarContent>
      <SidebarFooter>
        <NavUser />
      </SidebarFooter>
      <SidebarRail />
    </Sidebar>
  )
}
