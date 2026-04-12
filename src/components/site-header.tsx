import { Link } from '@tanstack/react-router'

import { Button } from '~/components/ui/button'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '~/components/ui/breadcrumb'
import { Separator } from '~/components/ui/separator'
import { SidebarTrigger } from '~/components/ui/sidebar'

import { useTheme } from '~/context/theme-context'
import { IconMoon, IconSun } from '@tabler/icons-react'

import { Search } from '~/components/search'

export function SiteHeader() {
  const { theme, setTheme } = useTheme()

  return (
    <header className="sticky top-0 z-10 flex h-16 shrink-0 items-center gap-2 border-b bg-background/95 backdrop-blur transition-[width,height] ease-linear group-has-data-[collapsible=icon]/sidebar-wrapper:h-12">
      <div className="flex w-full items-center gap-2 px-4 lg:px-6">
        <SidebarTrigger className="-ml-1" />
        <Separator
          orientation="vertical"
          className="mr-2 data-[orientation=vertical]:h-4"
        />
        <Breadcrumb>
          <BreadcrumbList>
            <BreadcrumbItem className="hidden md:block">
              <BreadcrumbLink asChild>
                <Link to="/dashboard">Dashboard</Link>
              </BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator className="hidden md:block" />
            <BreadcrumbItem>
              <BreadcrumbPage>Overview</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>

        <div className="ml-auto flex items-center gap-2">
          <Search />

          <Button
            onClick={() =>
              theme === 'dark' ? setTheme('light') : setTheme('dark')
            }
            variant="ghost"
            size="sm"
            className="hidden sm:flex"
          >
            {theme === 'dark' ? (
              <IconSun stroke={2} />
            ) : (
              <IconMoon stroke={2} />
            )}
          </Button>

          <Button variant="ghost" asChild size="sm" className="hidden sm:flex">
            <a
              href="https://github.com/shadcn-ui/ui/tree/main/apps/v4/app/(examples)/dashboard"
              rel="noopener noreferrer"
              target="_blank"
              className="dark:text-foreground"
            >
              GitHub
            </a>
          </Button>
        </div>
      </div>
    </header>
  )
}
