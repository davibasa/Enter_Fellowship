"use client";

import { create } from 'zustand';

const useSidebarStore = create((set) => ({
  isOpen: true,
  isHovered: false,
  toggle: () => set((state) => ({ isOpen: !state.isOpen })),
  setOpen: (isOpen) => set({ isOpen }),
  setHovered: (isHovered) => set({ isHovered }),
}));

export const useSidebar = () => {
  const { isOpen, isHovered, toggle, setOpen, setHovered } = useSidebarStore();
  
  return {
    isOpen,
    isHovered,
    isExpanded: isOpen || isHovered,
    toggle,
    setOpen,
    setHovered,
  };
};
