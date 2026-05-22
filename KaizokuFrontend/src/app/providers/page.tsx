"use client";

import { Plug } from "lucide-react";
import React from 'react';

import { RibbonSlot } from "@/components/kzk/layout/ribbon";
import { ProviderManager } from "@/components/kzk/provider-manager";
import { useSearch } from "@/contexts/search-context";

export default function ProvidersPage() {
  const { searchTerm, setSearchTerm, clearSearch } = useSearch();

  return (
    <div className="space-y-6">
      {/*
        Sources contextual ribbon — page heading only. The Installed / Available
        filter chrome (search box, NSFW toggle, language multi-select, Check All
        button) still lives inside <ProviderManager> because it's interleaved
        with the two-column layout. Lifting all four pieces of state into the
        ribbon is a follow-up refactor.
      */}
      <RibbonSlot>
        <div className="flex w-full items-center gap-2">
          <Plug className="h-4 w-4 text-muted-foreground shrink-0" />
          <h2 className="truncate text-sm font-semibold text-foreground">
            Sources
          </h2>
          <span className="hidden sm:inline truncate text-xs text-muted-foreground">
            · Install, enable, and health-check Mihon extensions
          </span>
        </div>
      </RibbonSlot>

      <ProviderManager
        searchTerm={searchTerm}
        setSearchTerm={setSearchTerm}
        clearSearch={clearSearch}
        isCompact={true}
        showSearch={false}
        showNsfwIndicator={true}
        installedGridCols="grid-cols-1 sm:grid-cols-1 md:grid-cols-1 lg:grid-cols-2 xl:grid-cols-2 2xl:grid-cols-3"
        availableGridCols="grid-cols-1 sm:grid-cols-1 md:grid-cols-1 lg:grid-cols-2 xl:grid-cols-2 2xl:grid-cols-3"
        installedTitle="Installed"
        availableTitle="Available"
      />
    </div>
  );
}
