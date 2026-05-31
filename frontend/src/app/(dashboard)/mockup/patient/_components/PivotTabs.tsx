'use client';
import React, { useState } from 'react';

interface PivotTab {
  key: string;
  label: string;
  group?: string;
}

interface PivotTabsProps {
  tabs: PivotTab[];
  activeTab: string;
  onTabChange: (key: string) => void;
}

export default function PivotTabs({ tabs, activeTab, onTabChange }: PivotTabsProps) {
  const [hoveredTab, setHoveredTab] = useState<string | null>(null);

  // Group tabs
  const groups: { label: string | null; tabs: PivotTab[] }[] = [];
  let currentGroup: { label: string | null; tabs: PivotTab[] } = { label: null, tabs: [] };
  
  tabs.forEach(tab => {
    if (tab.group && tab.group !== currentGroup.label) {
      if (currentGroup.tabs.length > 0) {
        groups.push(currentGroup);
      }
      currentGroup = { label: tab.group, tabs: [tab] };
    } else {
      currentGroup.tabs.push(tab);
    }
  });
  if (currentGroup.tabs.length > 0) {
    groups.push(currentGroup);
  }

  return (
    <div className="bg-white border-b border-slate-200">
      <div className="px-4 overflow-x-auto">
        <div className="flex items-end gap-0 min-w-fit">
          {groups.map((group, gi) => (
            <React.Fragment key={gi}>
              {group.label && gi > 0 && (
                <div className="w-px h-6 bg-slate-200 self-center mx-1" />
              )}
              {group.label && (
                <div className="flex items-center px-2 pb-1">
                  <span className="text-[9px] font-bold text-slate-300 uppercase tracking-wider">
                    {group.label}
                  </span>
                </div>
              )}
              {group.tabs.map(tab => {
                const isActive = activeTab === tab.key;
                const isHovered = hoveredTab === tab.key;
                return (
                  <button
                    key={tab.key}
                    onClick={() => onTabChange(tab.key)}
                    onMouseEnter={() => setHoveredTab(tab.key)}
                    onMouseLeave={() => setHoveredTab(null)}
                    className={`
                      relative px-4 py-2.5 text-sm font-bold whitespace-nowrap transition-all duration-200
                      ${isActive 
                        ? 'text-sky-600' 
                        : isHovered 
                          ? 'text-slate-700 bg-slate-50' 
                          : 'text-slate-400 hover:text-slate-600'
                      }
                    `}
                  >
                    {tab.label}
                    {/* Active indicator - Microsoft Fluent style bottom bar */}
                    {isActive && (
                      <div className="absolute bottom-0 left-2 right-2 h-[3px] bg-sky-500 rounded-t-full" />
                    )}
                    {/* Hover indicator */}
                    {isHovered && !isActive && (
                      <div className="absolute bottom-0 left-2 right-2 h-[2px] bg-slate-200 rounded-t-full" />
                    )}
                  </button>
                );
              })}
            </React.Fragment>
          ))}
        </div>
      </div>
    </div>
  );
}
