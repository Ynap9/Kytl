import { NgTemplateOutlet } from '@angular/common';
import {
  Component,
  DestroyRef,
  ElementRef,
  TemplateRef,
  computed,
  contentChild,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
  viewChildren
} from '@angular/core';
import * as pdfjs from 'pdfjs-dist';
import type { PDFDocumentLoadingTask, PDFDocumentProxy } from 'pdfjs-dist';

import { IPdfPage, IPdfPageMetric } from '../../models/pdf-pages.models';

pdfjs.GlobalWorkerOptions.workerSrc = new URL('pdf.worker.min.mjs', document.baseURI).toString();

const PAGE_MIN_WIDTH_PX = 320;
const PAGE_MAX_WIDTH_PX = 1100;
const HOST_PADDING_PX = 40;
const RESIZE_SETTLE_MS = 250;
const RENDER_SCALE = Math.max(window.devicePixelRatio || 1, 2);
const RENDER_ROOT_MARGIN = '300px';

@Component({
  selector: 'app-pdf-pages',
  imports: [NgTemplateOutlet],
  templateUrl: './pdf-pages.html',
  styleUrl: './pdf-pages.scss'
})
export class PdfPages {
  private destroyRef = inject(DestroyRef);

  url = input<string | null>(null);
  busy = input<boolean>(false);
  emptyText = input<string>('Chưa có file để xem trước.');
  loaded = output<IPdfPageMetric[]>();
  failed = output<void>();

  pageOverlay = contentChild<TemplateRef<{ $implicit: IPdfPage }>>('pageOverlay');

  loading = signal<boolean>(false);
  private metrics = signal<IPdfPageMetric[]>([]);
  private pageWidth = signal<number>(PAGE_MAX_WIDTH_PX);

  pages = computed<IPdfPage[]>(() => {
    const cssWidth = this.pageWidth();
    return this.metrics().map((metric) => ({
      ...metric,
      cssWidth,
      cssHeight: (cssWidth * metric.heightPt) / metric.widthPt
    }));
  });

  private wrap = viewChild<ElementRef<HTMLDivElement>>('wrap');
  private canvases = viewChildren<ElementRef<HTMLCanvasElement>>('canvas');
  private doc: PDFDocumentProxy | null = null;
  private task: PDFDocumentLoadingTask | null = null;
  private loadVersion = 0;
  private rendered = new Set<number>();
  private pageObserver: IntersectionObserver | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private resizeTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this.destroyRef.onDestroy(() => {
      this.pageObserver?.disconnect();
      this.resizeObserver?.disconnect();
      if (this.resizeTimer) clearTimeout(this.resizeTimer);
      void this.task?.destroy();
    });
  }

  private urlEffect = effect(() => {
    const url = this.url();
    untracked(() => void this.open(url));
  });

  private resizeEffect = effect(() => {
    const host = this.wrap()?.nativeElement;
    if (!host) return;

    this.resizeObserver?.disconnect();
    this.resizeObserver = new ResizeObserver(() => {
      const usable = host.clientWidth - HOST_PADDING_PX;
      this.pageWidth.set(Math.min(Math.max(usable, PAGE_MIN_WIDTH_PX), PAGE_MAX_WIDTH_PX));

      if (this.resizeTimer) clearTimeout(this.resizeTimer);
      this.resizeTimer = setTimeout(() => {
        this.rendered.clear();
        this.observePages();
      }, RESIZE_SETTLE_MS);
    });
    this.resizeObserver.observe(host);
  });

  private observeEffect = effect(() => {
    const canvases = this.canvases();
    const pages = this.pages();
    if (pages.length === 0 || canvases.length !== pages.length) return;

    untracked(() => {
      this.pageObserver?.disconnect();
      this.pageObserver = new IntersectionObserver(
        (entries) => {
          for (const entry of entries) {
            if (!entry.isIntersecting) continue;
            void this.renderPage(Number((entry.target as HTMLElement).dataset['page']));
          }
        },
        { rootMargin: RENDER_ROOT_MARGIN }
      );
      this.observePages();
    });
  });

  private observePages(): void {
    const observer = this.pageObserver;
    if (!observer) return;
    observer.disconnect();
    this.canvases().forEach((canvas) => observer.observe(canvas.nativeElement));
  }

  private async open(url: string | null): Promise<void> {
    const version = ++this.loadVersion;
    this.pageObserver?.disconnect();
    this.rendered.clear();
    this.metrics.set([]);
    this.doc = null;
    void this.task?.destroy();
    this.task = null;

    if (!url) {
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    try {
      const task = pdfjs.getDocument({ url });
      this.task = task;
      const doc = await task.promise;

      const metrics: IPdfPageMetric[] = [];
      for (let number = 1; number <= doc.numPages; number++) {
        const viewport = (await doc.getPage(number)).getViewport({ scale: 1 });
        metrics.push({ number, widthPt: viewport.width, heightPt: viewport.height });
      }

      if (version !== this.loadVersion) return;
      this.doc = doc;
      this.metrics.set(metrics);
      this.loaded.emit(metrics);
    } catch {
      if (version === this.loadVersion) this.failed.emit();
    } finally {
      if (version === this.loadVersion) this.loading.set(false);
    }
  }

  private async renderPage(pageNumber: number): Promise<void> {
    const doc = this.doc;
    const meta = this.pages().find((page) => page.number === pageNumber);
    const canvas = this.canvases().find(
      (ref) => Number(ref.nativeElement.dataset['page']) === pageNumber
    )?.nativeElement;
    if (!doc || !meta || !canvas || this.rendered.has(pageNumber)) return;

    this.rendered.add(pageNumber);
    const page = await doc.getPage(pageNumber);
    const viewport = page.getViewport({ scale: (meta.cssWidth * RENDER_SCALE) / meta.widthPt });
    canvas.width = viewport.width;
    canvas.height = viewport.height;

    const context = canvas.getContext('2d');
    if (context) {
      await page.render({ canvas, canvasContext: context, viewport }).promise;
    }
  }
}
