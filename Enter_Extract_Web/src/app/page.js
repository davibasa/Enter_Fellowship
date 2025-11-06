"use client";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { FileText, Zap, Shield, TrendingUp, ArrowRight } from "lucide-react";
import Link from "next/link";

export default function Home() {
  return (
    <div className="space-y-12 animate-fade-in">
      {/* Hero Section */}
      <div className="text-center space-y-4 py-12">
        <div className="inline-flex h-16 w-16 items-center justify-center rounded-2xl bg-primary/10 mb-4">
          <Zap className="h-8 w-8 text-primary" />
        </div>
        <h1 className="text-4xl font-bold tracking-tight sm:text-6xl">
          Welcome to Enter Extract
        </h1>
        <p className="text-xl text-muted-foreground max-w-2xl mx-auto">
          Advanced document extraction and processing powered by AI. Extract data
          from PDFs, invoices, contracts, and more with ease.
        </p>
        <div className="flex items-center justify-center gap-4 pt-4">
          <Link href="/dashboard">
            <Button size="lg" className="gap-2">
              Get Started
              <ArrowRight className="h-4 w-4" />
            </Button>
          </Link>
          <Button size="lg" variant="outline">
            Learn More
          </Button>
        </div>
      </div>

      {/* Features Grid */}
      <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
        <Card className="hover:shadow-lg transition-shadow">
          <CardHeader>
            <div className="h-12 w-12 rounded-lg bg-primary/10 flex items-center justify-center mb-2">
              <FileText className="h-6 w-6 text-primary" />
            </div>
            <CardTitle>Smart Extraction</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-muted-foreground">
              Automatically extract key information from documents using advanced
              AI and machine learning algorithms.
            </p>
          </CardContent>
        </Card>

        <Card className="hover:shadow-lg transition-shadow">
          <CardHeader>
            <div className="h-12 w-12 rounded-lg bg-primary/10 flex items-center justify-center mb-2">
              <Zap className="h-6 w-6 text-primary" />
            </div>
            <CardTitle>Lightning Fast</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-muted-foreground">
              Process thousands of documents in seconds with our optimized
              extraction pipeline and caching system.
            </p>
          </CardContent>
        </Card>

        <Card className="hover:shadow-lg transition-shadow">
          <CardHeader>
            <div className="h-12 w-12 rounded-lg bg-primary/10 flex items-center justify-center mb-2">
              <Shield className="h-6 w-6 text-primary" />
            </div>
            <CardTitle>Secure & Reliable</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-muted-foreground">
              Enterprise-grade security with end-to-end encryption and 99.9%
              uptime guarantee for your critical data.
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Stats Section */}
      <Card className="bg-primary text-primary-foreground">
        <CardContent className="pt-6">
          <div className="grid gap-8 md:grid-cols-3">
            <div className="text-center">
              <div className="flex items-center justify-center gap-2 mb-2">
                <TrendingUp className="h-5 w-5" />
                <div className="text-4xl font-bold">98.5%</div>
              </div>
              <p className="text-sm opacity-90">Accuracy Rate</p>
            </div>
            <div className="text-center">
              <div className="flex items-center justify-center gap-2 mb-2">
                <FileText className="h-5 w-5" />
                <div className="text-4xl font-bold">1M+</div>
              </div>
              <p className="text-sm opacity-90">Documents Processed</p>
            </div>
            <div className="text-center">
              <div className="flex items-center justify-center gap-2 mb-2">
                <Zap className="h-5 w-5" />
                <div className="text-4xl font-bold">2.4s</div>
              </div>
              <p className="text-sm opacity-90">Average Processing Time</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* CTA Section */}
      <Card>
        <CardContent className="pt-6">
          <div className="text-center space-y-4">
            <h2 className="text-2xl font-bold">Ready to get started?</h2>
            <p className="text-muted-foreground">
              Join thousands of businesses already using Enter Extract
            </p>
            <Link href="/dashboard">
              <Button size="lg" className="gap-2">
                Start Extracting
                <ArrowRight className="h-4 w-4" />
              </Button>
            </Link>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
