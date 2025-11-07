"use client";

import { cn } from "@/lib/utils";
import { useSidebar } from "@/hooks/use-sidebar";
import { useMobile } from "@/hooks/use-mobile";
import { Button } from "@/components/ui/button";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Separator } from "@/components/ui/separator";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import {
  LayoutDashboard,
  BarChart3,
  Settings,
  FileText,
  Users,
  Zap,
  ChevronLeft,
  ChevronRight,
  LogOut,
  Home,
  Database,
} from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";

const menuItems = [
  {
    title: "Dashboard",
    icon: LayoutDashboard,
    href: "/dashboard",
  },
  {
    title: "Documentos",
    icon: FileText,
    href: "/documents",
  },
  {
    title: "Padrões dos PDFs",
    icon: Database,
    href: "/schema-patterns",
  }
];

export function Sidebar() {
  const { isOpen, isHovered, isExpanded, toggle, setHovered } = useSidebar();
  const isMobile = useMobile();
  const pathname = usePathname();

  return (
    <>
      {/* Overlay para mobile */}
      {isMobile && isOpen && (
        <div
          className="fixed inset-0 z-40 bg-black/50 backdrop-blur-sm animate-fade-in"
          onClick={toggle}
        />
      )}

      {/* Sidebar */}
      <aside
        onMouseEnter={() => !isMobile && setHovered(true)}
        onMouseLeave={() => !isMobile && setHovered(false)}
        className={cn(
          "fixed left-0 top-0 z-50 h-screen bg-card border-r border-border transition-all duration-300 ease-in-out",
          isExpanded ? "w-64" : "w-20",
          isMobile && !isOpen && "-translate-x-full"
        )}
      >
        <div className="flex h-full flex-col">
          {/* Header */}
          <div className="flex h-16 items-center justify-between px-4 border-b border-border">
            <div
              className={cn(
                "flex items-center gap-2 transition-opacity duration-200",
                isExpanded ? "opacity-100" : "opacity-0"
              )}
            >
              <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
                <Zap className="h-5 w-5" />
              </div>
              <span className="font-semibold text-lg">Enter Extract</span>
            </div>

            {!isMobile && (
              <Button
                variant="ghost"
                size="icon"
                onClick={toggle}
                className={cn(
                  "h-8 w-8 transition-transform duration-200",
                  !isExpanded && "rotate-180"
                )}
              >
                <ChevronLeft className="h-4 w-4" />
              </Button>
            )}
          </div>

          {/* Navigation */}
          <ScrollArea className="flex-1 px-3 py-4">
            <nav className="space-y-1">
              {menuItems.map((item) => {
                const Icon = item.icon;
                const isActive = pathname === item.href;

                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    className={cn(
                      "flex items-center gap-3 rounded-lg px-3 py-2 transition-all duration-200",
                      "hover:bg-accent hover:text-accent-foreground",
                      isActive
                        ? "bg-primary text-primary-foreground shadow-sm"
                        : "text-muted-foreground",
                      !isExpanded && "justify-center"
                    )}
                  >
                    <Icon className="h-5 w-5 shrink-0" />
                    <span
                      className={cn(
                        "transition-opacity duration-200 whitespace-nowrap",
                        isExpanded ? "opacity-100" : "opacity-0 w-0"
                      )}
                    >
                      {item.title}
                    </span>
                  </Link>
                );
              })}
            </nav>
          </ScrollArea>

          <Separator />

          {/* Footer */}
          <div className="p-4">
            <div
              className={cn(
                "flex items-center gap-3 rounded-lg p-2 hover:bg-accent transition-colors cursor-pointer",
                !isExpanded && "justify-center"
              )}
            >
              <Avatar className="h-8 w-8 border-2 border-primary">
                <AvatarImage src="/avatar.jpg" alt="User" />
                <AvatarFallback className="bg-primary text-primary-foreground text-xs">
                  DB
                </AvatarFallback>
              </Avatar>
              <div
                className={cn(
                  "transition-opacity duration-200 min-w-0",
                  isExpanded ? "opacity-100" : "opacity-0 w-0"
                )}
              >
                <p className="text-sm font-medium truncate">Davi Basa</p>
                <p className="text-xs text-muted-foreground truncate">
                  admin@enter.com
                </p>
              </div>
              {isExpanded && (
                <Button
                  variant="ghost"
                  size="icon"
                  className="ml-auto h-8 w-8"
                >
                  <LogOut className="h-4 w-4" />
                </Button>
              )}
            </div>
          </div>
        </div>
      </aside>
    </>
  );
}
