"use client";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utills";

export function StatsCard({ title, value, icon: Icon, trend, description, className }) {
  const isPositive = trend && trend > 0;

  return (
    <Card className={cn("hover:shadow-lg transition-shadow", className)}>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">
          {title}
        </CardTitle>
        {Icon && (
          <div className="h-10 w-10 rounded-full bg-primary/10 flex items-center justify-center">
            <Icon className="h-5 w-5 text-primary" />
          </div>
        )}
      </CardHeader>
      <CardContent>
        <div className="text-3xl font-bold">{value}</div>
        {trend !== undefined && (
          <p className="text-xs text-muted-foreground mt-2">
            <span
              className={cn(
                "font-medium",
                isPositive ? "text-green-600" : "text-red-600"
              )}
            >
              {isPositive ? "+" : ""}
              {trend}%
            </span>{" "}
            {description || "from last month"}
          </p>
        )}
      </CardContent>
    </Card>
  );
}
