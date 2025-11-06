"use client";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";

const activities = [
  {
    id: 1,
    user: "John Doe",
    avatar: "/avatars/01.jpg",
    action: "uploaded a new document",
    time: "2 minutes ago",
    status: "success",
  },
  {
    id: 2,
    user: "Jane Smith",
    avatar: "/avatars/02.jpg",
    action: "extracted data from PDF",
    time: "10 minutes ago",
    status: "success",
  },
  {
    id: 3,
    user: "Mike Johnson",
    avatar: "/avatars/03.jpg",
    action: "failed to process document",
    time: "1 hour ago",
    status: "error",
  },
  {
    id: 4,
    user: "Sarah Williams",
    avatar: "/avatars/04.jpg",
    action: "created new extraction template",
    time: "2 hours ago",
    status: "info",
  },
  {
    id: 5,
    user: "Tom Brown",
    avatar: "/avatars/05.jpg",
    action: "updated system settings",
    time: "3 hours ago",
    status: "warning",
  },
];

const statusColors = {
  success: "bg-green-500",
  error: "bg-red-500",
  warning: "bg-yellow-500",
  info: "bg-blue-500",
};

export function RecentActivity() {
  return (
    <Card className="col-span-1">
      <CardHeader>
        <CardTitle>Recent Activity</CardTitle>
      </CardHeader>
      <CardContent>
        <ScrollArea className="h-[400px] pr-4">
          <div className="space-y-4">
            {activities.map((activity) => (
              <div key={activity.id} className="flex items-start gap-4">
                <div className="relative">
                  <Avatar className="h-9 w-9">
                    <AvatarImage src={activity.avatar} alt={activity.user} />
                    <AvatarFallback>
                      {activity.user
                        .split(" ")
                        .map((n) => n[0])
                        .join("")}
                    </AvatarFallback>
                  </Avatar>
                  <span
                    className={`absolute bottom-0 right-0 h-3 w-3 rounded-full border-2 border-background ${
                      statusColors[activity.status]
                    }`}
                  />
                </div>
                <div className="flex-1 space-y-1">
                  <p className="text-sm">
                    <span className="font-medium">{activity.user}</span>{" "}
                    <span className="text-muted-foreground">
                      {activity.action}
                    </span>
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {activity.time}
                  </p>
                </div>
              </div>
            ))}
          </div>
        </ScrollArea>
      </CardContent>
    </Card>
  );
}
