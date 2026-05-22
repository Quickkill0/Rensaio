"use client";

import React from "react";
import { ChevronLeft } from "lucide-react";
import { Button } from "@/components/ui/button";
import { RibbonSlot } from "@/components/kzk/layout/ribbon";

interface SeriesRibbonProps {
  seriesTitle: string;
  onBack: () => void;
}

export function SeriesRibbon({ seriesTitle, onBack }: SeriesRibbonProps) {
  return (
    <RibbonSlot>
      <div className="flex w-full items-center gap-3">
        {/* Left: back button */}
        <Button
          onClick={onBack}
          variant="ghost"
          size="sm"
          className="h-8 gap-1.5 px-2 -ml-1"
        >
          <ChevronLeft className="h-4 w-4" />
          <span className="hidden sm:inline">Back to Library</span>
        </Button>

        {/* Center: title (lg+ only, truncate) */}
        <div className="hidden lg:flex flex-1 min-w-0 justify-center">
          <span
            className="truncate text-sm font-medium text-foreground/80 max-w-[60%]"
            title={seriesTitle}
          >
            {seriesTitle}
          </span>
        </div>

        {/* Mobile spacer */}
        <div className="flex-1 lg:hidden" />
      </div>
    </RibbonSlot>
  );
}
