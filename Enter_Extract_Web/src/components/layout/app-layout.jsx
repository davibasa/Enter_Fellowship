"use client";

import { cn } from "@/lib/utils";
import { Sidebar } from "./sidebar";
import { Header } from "./header";
import { useSidebar } from "@/hooks/use-sidebar";
import { useMobile } from "@/hooks/use-mobile";

export function AppLayout({ children }) {
  const { isExpanded } = useSidebar();
  const isMobile = useMobile();

  return (
    <div className="relative min-h-screen bg-background">
      <Sidebar />
      <Header />
      
      <main
        className={cn(
          "pt-16 transition-all duration-300 min-h-screen",
          isExpanded ? "ml-64" : "ml-20",
          isMobile && "ml-0"
        )}
      >
        <div className="p-6">
          {children}
        </div>
      </main>
    </div>
  );
}
