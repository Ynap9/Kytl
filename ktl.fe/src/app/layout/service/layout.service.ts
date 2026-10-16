import { Injectable, isDevMode } from '@angular/core';

const DESIGN_WIDTH_PX = 1440;
const MIN_ZOOM = 0.6;

@Injectable({ providedIn: 'root' })
export class LayoutService {
  constructor() {
    this.applyZoom();
    window.addEventListener('resize', () => this.applyZoom());
    if (!isDevMode()) {
      this.blockBrowserBehaviors();
    }
  }

  private blockBrowserBehaviors(): void {
    document.addEventListener('contextmenu', (event) => {
      const target = event.target as HTMLElement | null;
      if (!target?.closest('input, textarea, [contenteditable="true"]')) {
        event.preventDefault();
      }
    });

    window.addEventListener(
      'wheel',
      (event) => {
        if (event.ctrlKey) event.preventDefault();
      },
      { passive: false }
    );

    document.addEventListener('keydown', (event) => {
      const key = event.key.toLowerCase();
      const withCtrl = event.ctrlKey || event.metaKey;
      if (
        key === 'f5' ||
        (withCtrl && ['r', 'p', 'u', 'f', 'g', 'j', '+', '-', '=', '0'].includes(key)) ||
        (event.altKey && (key === 'arrowleft' || key === 'arrowright'))
      ) {
        event.preventDefault();
      }
    });
  }

  private applyZoom(): void {
    const zoom = Math.min(1, Math.max(window.innerWidth / DESIGN_WIDTH_PX, MIN_ZOOM));
    const root = document.documentElement;
    root.style.setProperty('zoom', String(zoom));
    root.style.setProperty('--app-vh', `${window.innerHeight / zoom}px`);
  }
}
