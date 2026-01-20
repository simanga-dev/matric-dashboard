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
  IconInnerShadowTop,
  IconListDetails,
  IconReport,
  IconSearch,
  IconSettings,
  IconTrophy,
} from '@tabler/icons-react'

import { NavDocuments } from '~/components/nav-documents'
import { NavMain } from '~/components/nav-main'
import { NavSecondary } from '~/components/nav-secondary'
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
} from '~/components/ui/sidebar'

const data = {
  user: {
    name: 'shadcn',
    email: 'm@example.com',
    avatar: '/avatars/shadcn.jpg',
  },
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
  navSecondary: [
    {
      title: 'Settings',
      url: '#',
      icon: IconSettings,
    },
    {
      title: 'Get Help',
      url: '#',
      icon: IconHelp,
    },
    {
      title: 'Search',
      url: '#',
      icon: IconSearch,
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
      url: 'dashboard/seed-data',
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
    <Sidebar collapsible="offcanvas" {...props}>
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton
              asChild
              className="data-[slot=sidebar-menu-button]:!p-1.5"
            >
              <a href="/">
                <IconInnerShadowTop className="!size-5" />
                <span className="text-base font-semibold">
                  Matric Dashboard
                </span>
              </a>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>
      <SidebarContent>
        <NavMain items={data.navMain} />
        <NavDocuments items={data.documents} />

        {isDevelopment && <NavDevelopment items={data.navDevelopmet} />}
        <NavSecondary items={data.navSecondary} className="mt-auto" />
      </SidebarContent>
      <SidebarFooter>
        <NavUser user={data.user} />
      </SidebarFooter>
    </Sidebar>
  )
}
