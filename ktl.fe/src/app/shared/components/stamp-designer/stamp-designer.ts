import { Component, TemplateRef, computed, effect, inject, input, model, output, signal, untracked } from '@angular/core';
import { SelectButtonModule } from 'primeng/selectbutton';
import { ToggleSwitchModule } from 'primeng/toggleswitch';

import { SharedImports } from '../../import.shared';
import { BaseComponent } from '../../components/base/base-component';
import { PdfPages } from '../../components/pdf-pages/pdf-pages';
import { IPdfPage, IPdfPageMetric } from '../../models/pdf-pages.models';
import { Utils } from '../../utils';
import { FilePickerService } from '../../../services/file-picker.service';
import { DocumentMergeService } from '../../../services/document-merge.service';
import { IMergeStamp, IMergeStampArea } from '../../../models/document-merge.models';
import {
  EMergePreviewMode,
  EMergeStampEditScope,
  EMergeStampKind,
  EMergeStampRepeatMode,
  EMergeStampScope,
  MERGE_CUSTOM_PAGE_PATTERN,
  MERGE_CUSTOM_PAGE_SEPARATOR,
  MERGE_PAGE_CHIP_LIMIT,
  MERGE_PREVIEW_MODE_OPTIONS,
  MERGE_STAMP_EDIT_SCOPE_OPTIONS,
  MERGE_STAMP_KIND_LABELS,
  MERGE_STAMP_REPEAT_OPTIONS,
  MERGE_STAMP_SCOPE_OPTIONS
} from '../../constants/document-merge.constants';
import {
  SIGN_BLOCK_MAX_SCALE,
  SIGN_BLOCK_MIN_SCALE,
  SIGN_IMAGE_BLOCK_WIDTH_PT
} from '../../constants/sign.constants';

type DragMode = 'move' | 'nw' | 'ne' | 'sw' | 'se';

interface IStampItem {
  index: number;
  page: number;
  stamp: IMergeStamp;
  area: IMergeStampArea;
  ghost: boolean;
  own: boolean;
  excluded: boolean;
}

@Component({
  selector: 'app-stamp-designer',
  imports: [...SharedImports, SelectButtonModule, ToggleSwitchModule, PdfPages],
  templateUrl: './stamp-designer.html',
  styleUrl: './stamp-designer.scss',
  host: { class: 'flex min-h-0 flex-1 gap-4' }
})
export class StampDesigner extends BaseComponent {
  private documentMergeService = inject(DocumentMergeService);
  protected filePicker = inject(FilePickerService);

  previewUrl = input<string | null>(null);
  previewLoading = input<boolean>(false);
  previewWarnings = input<string[]>([]);
  emptyText = input<string>('Đang tạo bản xem trước.');
  toggleable = input<boolean>(true);
  title = input<string>('Dấu & chữ ký');
  subtitle = input<string>('Đóng lên PDF sau khi nhồi dữ liệu');
  modeSwitch = input<boolean>(true);
  imageFilters = input<Partial<Record<EMergeStampKind, string>>>({});
  pageExtra = input<TemplateRef<{ $implicit: IPdfPage }> | null>(null);
  pagesLoaded = output<IPdfPageMetric[]>();
  enabled = model<boolean>(false);
  mode = model<EMergePreviewMode>(EMergePreviewMode.Edit);
  sealImagePath = model<string>('');
  wetSignatureImagePath = model<string>('');
  stamps = model<IMergeStamp[]>([]);

  protected readonly EKind = EMergeStampKind;
  protected readonly EScope = EMergeStampScope;
  protected readonly ERepeat = EMergeStampRepeatMode;
  protected readonly EMode = EMergePreviewMode;
  protected readonly kinds = [EMergeStampKind.Seal, EMergeStampKind.WetSignature];
  protected readonly scopeOptions = MERGE_STAMP_SCOPE_OPTIONS;
  protected readonly repeatOptions = MERGE_STAMP_REPEAT_OPTIONS;
  protected readonly editScopeOptions = MERGE_STAMP_EDIT_SCOPE_OPTIONS;
  protected readonly resizeCorners: DragMode[] = ['nw', 'ne', 'sw', 'se'];

  pageMetrics = signal<IPdfPageMetric[]>([]);
  activeIndex = signal<number>(-1);
  activePage = signal<number | null>(null);
  editScope = signal<EMergeStampEditScope>(EMergeStampEditScope.AllPages);
  imageAspects = signal<Partial<Record<EMergeStampKind, number>>>({});
  tileDragKind = signal<EMergeStampKind | null>(null);
  uploadingKind = signal<EMergeStampKind | null>(null);

  modeOptions = computed(() =>
    MERGE_PREVIEW_MODE_OPTIONS.map((option) => ({
      ...option,
      disabled: option.value === EMergePreviewMode.Final && (!this.enabled() || this.stamps().length === 0)
    }))
  );

  pageCount = computed(() => this.pageMetrics().length);
  pageNumbers = computed(() => Array.from({ length: this.pageCount() }, (_, i) => i + 1));
  activeStamp = computed<IMergeStamp | null>(() => this.stamps()[this.activeIndex()] ?? null);
  activeCopyPage = computed(() => this.activePage() ?? this.activeStamp()?.pageNumber ?? null);
  activeArea = computed<IMergeStampArea | null>(() => {
    const stamp = this.activeStamp();
    const page = this.activeCopyPage();
    return stamp && page !== null ? this.areaOf(stamp, page) : null;
  });
  showPageChips = computed(() => this.pageCount() <= MERGE_PAGE_CHIP_LIMIT);
  canPlace = computed(
    () => this.enabled() && this.mode() === EMergePreviewMode.Edit && this.pageCount() > 0 && !this.previewLoading()
  );

  private paletteKind = EMergeStampKind.Seal;
  private paletteGrabX = 0;
  private paletteGrabY = 0;
  private drag: {
    mode: DragMode;
    index: number;
    page: number;
    toPage: boolean;
    host: HTMLElement;
    grabX: number;
    grabY: number;
  } | null = null;

  private disabledModeEffect = effect(() => {
    const enabled = this.enabled();
    untracked(() => {
      if (!enabled && this.mode() === EMergePreviewMode.Final) this.mode.set(EMergePreviewMode.Edit);
    });
  });

  private aspectEffect = effect(() => {
    const seal = this.sealImagePath();
    const wetSignature = this.wetSignatureImagePath();
    untracked(() => {
      this.loadImageAspect(EMergeStampKind.Seal, seal);
      this.loadImageAspect(EMergeStampKind.WetSignature, wetSignature);
    });
  });

  kindLabel(kind: EMergeStampKind): string {
    return MERGE_STAMP_KIND_LABELS[kind] ?? '';
  }

  imagePath(kind: EMergeStampKind): string {
    return kind === EMergeStampKind.Seal ? this.sealImagePath() : this.wetSignatureImagePath();
  }

  imageUrl(kind: EMergeStampKind): string | null {
    const path = this.imagePath(kind);
    return path ? this.documentMergeService.imageUrl(path) : null;
  }

  imageFilter(kind: EMergeStampKind): string | null {
    return this.imageFilters()[kind] ?? null;
  }

  stampCount(kind: EMergeStampKind): number {
    return this.stamps().filter((stamp) => stamp.kind === kind).length;
  }

  async browseImage(kind: EMergeStampKind): Promise<void> {
    if (!this.filePicker.canBrowse) {
      this.messageWarning('Hộp thoại chọn ảnh chỉ có trên ứng dụng desktop. Hãy kéo thả hoặc dán đường dẫn để thử trên web.');
      return;
    }
    const path = await this.filePicker.browse('image');
    if (path) this.onImagePathChange(kind, path);
  }

  onTileDragOver(event: DragEvent, kind: EMergeStampKind): void {
    if (!event.dataTransfer?.types.includes('Files')) return;
    event.preventDefault();
    this.tileDragKind.set(kind);
  }

  onTileDragLeave(): void {
    this.tileDragKind.set(null);
  }

  onTileDrop(event: DragEvent, kind: EMergeStampKind): void {
    const file = event.dataTransfer?.files?.[0];
    this.tileDragKind.set(null);
    if (!file) return;
    event.preventDefault();

    if (!this.filePicker.accepts('image', file.name)) {
      this.messageWarning(`Chỉ nhận ảnh ${this.filePicker.extensions('image').join(', ')}.`);
      return;
    }

    this.uploadingKind.set(kind);
    this.filePicker.upload(file).subscribe({
      next: (res) => {
        this.uploadingKind.set(null);
        if (this.isResponseSucceed(res)) this.onImagePathChange(kind, res.data.filePath);
      },
      error: () => {
        this.uploadingKind.set(null);
        this.messageError('Không đưa được ảnh vào ứng dụng. Vui lòng thử lại hoặc bấm để chọn ảnh.');
      }
    });
  }

  onImagePathChange(kind: EMergeStampKind, path: string): void {
    const value = Utils.normalizeFolderPath(path ?? '');
    if (kind === EMergeStampKind.Seal) this.sealImagePath.set(value);
    else this.wetSignatureImagePath.set(value);

    if (!value) {
      this.stamps.update((list) => list.filter((stamp) => stamp.kind !== kind));
      this.activeIndex.set(-1);
    }
  }

  onModeChange(mode: EMergePreviewMode | null): void {
    if (mode !== null) this.mode.set(mode);
  }

  onPdfLoaded(metrics: IPdfPageMetric[]): void {
    this.pageMetrics.set(metrics);
    this.pagesLoaded.emit(metrics);
  }

  onPdfFailed(): void {
    this.messageError('Không đọc được file xem trước. Vui lòng bấm nút tạo lại bản xem trước.');
  }

  private loadImageAspect(kind: EMergeStampKind, path: string): void {
    if (!path) return;
    const image = new Image();
    image.onload = () => {
      if (image.naturalWidth > 0) {
        this.imageAspects.update((map) => ({ ...map, [kind]: image.naturalHeight / image.naturalWidth }));
      }
    };
    image.onerror = () =>
      this.messageError(
        `Không mở được ảnh ${this.kindLabel(kind).toLowerCase()}. Kiểm tra lại file có tồn tại và là ảnh PNG hoặc JPEG.`
      );
    image.src = this.documentMergeService.imageUrl(path);
  }

  private baseSize(kind: EMergeStampKind, page: IPdfPageMetric): { width: number; height: number } {
    return {
      width: Math.min(SIGN_IMAGE_BLOCK_WIDTH_PT / page.widthPt, 1),
      height: Math.min((SIGN_IMAGE_BLOCK_WIDTH_PT * (this.imageAspects()[kind] ?? 1)) / page.heightPt, 1)
    };
  }

  resolvePages(stamp: IMergeStamp): number[] {
    const count = this.pageCount();
    if (stamp.scope === EMergeStampScope.CurrentPage) {
      return stamp.pageNumber <= count ? [stamp.pageNumber] : [];
    }

    const pages = Array.from({ length: count }, (_, i) => i + 1);
    switch (stamp.repeatMode) {
      case EMergeStampRepeatMode.Even:
        return pages.filter((page) => page % 2 === 0);
      case EMergeStampRepeatMode.Odd:
        return pages.filter((page) => page % 2 === 1);
      case EMergeStampRepeatMode.Custom: {
        const ranges = this.parseCustomPages(stamp.customPages);
        return ranges ? pages.filter((page) => ranges.some((r) => page >= r.from && page <= r.to)) : [];
      }
      default:
        return pages;
    }
  }

  customPagesError(stamp: IMergeStamp): string | null {
    if (stamp.scope !== EMergeStampScope.Repeat || stamp.repeatMode !== EMergeStampRepeatMode.Custom) return null;
    return this.parseCustomPages(stamp.customPages) ? null : 'Nhập danh sách trang dạng 1,3,5-7.';
  }

  private parseCustomPages(value: string | null): { from: number; to: number }[] | null {
    const parts = (value ?? '')
      .split(MERGE_CUSTOM_PAGE_SEPARATOR)
      .map((part) => part.trim())
      .filter((part) => part);
    if (parts.length === 0) return null;

    const ranges: { from: number; to: number }[] = [];
    for (const part of parts) {
      const match = MERGE_CUSTOM_PAGE_PATTERN.exec(part);
      if (!match) return null;
      const from = Number(match[1]);
      const to = match[2] ? Number(match[2]) : from;
      if (from < 1 || to < from) return null;
      ranges.push({ from, to });
    }
    return ranges;
  }

  stampSummary(stamp: IMergeStamp): string {
    if (stamp.scope === EMergeStampScope.CurrentPage) return 'Chỉ trang đặt';
    const label = this.repeatOptions.find((option) => option.value === stamp.repeatMode)?.label ?? '';
    return stamp.repeatMode === EMergeStampRepeatMode.Custom ? `Trang ${stamp.customPages || '—'}` : `Lặp ${label.toLowerCase()}`;
  }

  togglePage(page: number): void {
    const stamp = this.activeStamp();
    if (!stamp) return;

    const pages = new Set(this.resolvePages(stamp));
    if (pages.has(page)) pages.delete(page);
    else pages.add(page);

    this.updateActiveStamp({
      scope: EMergeStampScope.Repeat,
      repeatMode: EMergeStampRepeatMode.Custom,
      customPages: this.compressPages([...pages])
    });
  }

  private compressPages(pages: number[]): string {
    const sorted = [...pages].sort((a, b) => a - b);
    const parts: string[] = [];
    for (let i = 0; i < sorted.length; i++) {
      const from = sorted[i];
      while (i + 1 < sorted.length && sorted[i + 1] === sorted[i] + 1) i++;
      parts.push(from === sorted[i] ? `${from}` : `${from}-${sorted[i]}`);
    }
    return parts.join(MERGE_CUSTOM_PAGE_SEPARATOR);
  }

  pageItems(page: IPdfPage): IStampItem[] {
    return this.stamps().flatMap((stamp, index): IStampItem[] => {
      const applied = this.resolvePages(stamp).includes(page.number);
      const origin = stamp.pageNumber === page.number;
      if (!origin && !applied) return [];
      return [
        {
          index,
          page: page.number,
          stamp,
          area: this.areaOf(stamp, page.number),
          ghost: !origin,
          own: this.hasOwnPosition(stamp, page.number),
          excluded: origin && !applied
        }
      ];
    });
  }

  areaOf(stamp: IMergeStamp, page: number): IMergeStampArea {
    return stamp.pagePositions.find((position) => position.pageNumber === page) ?? stamp;
  }

  hasOwnPosition(stamp: IMergeStamp, page: number): boolean {
    return stamp.pagePositions.some((position) => position.pageNumber === page);
  }

  ownPages(stamp: IMergeStamp): number[] {
    const applied = this.resolvePages(stamp);
    return stamp.pagePositions
      .map((position) => position.pageNumber)
      .filter((page) => applied.includes(page))
      .sort((a, b) => a - b);
  }

  appliedSummary(stamp: IMergeStamp): string {
    return this.compressPages(this.resolvePages(stamp)) || 'không có';
  }

  isActiveCopy(item: IStampItem): boolean {
    return item.index === this.activeIndex() && item.page === this.activeCopyPage();
  }

  resetPagePosition(page: number): void {
    const stamp = this.activeStamp();
    if (!stamp) return;
    this.updateActiveStamp({ pagePositions: stamp.pagePositions.filter((position) => position.pageNumber !== page) });
  }

  resetPagePositions(): void {
    this.updateActiveStamp({ pagePositions: [] });
  }

  private writesToPage(stamp: IMergeStamp, page: number): boolean {
    if (stamp.scope !== EMergeStampScope.Repeat) return false;
    return this.editScope() === EMergeStampEditScope.CurrentPage || this.hasOwnPosition(stamp, page);
  }

  private applyArea(index: number, page: number, toPage: boolean, area: IMergeStampArea): void {
    this.stamps.update((list) =>
      list.map((stamp, i) => {
        if (i !== index) return stamp;
        if (!toPage) return { ...stamp, ...area };
        const others = stamp.pagePositions.filter((position) => position.pageNumber !== page);
        return { ...stamp, pagePositions: [...others, { pageNumber: page, ...area }] };
      })
    );
  }

  selectStamp(index: number): void {
    this.activeIndex.set(index);
    this.activePage.set(null);
  }

  updateActiveStamp(patch: Partial<IMergeStamp>): void {
    const index = this.activeIndex();
    if (index < 0) return;
    this.stamps.update((list) => list.map((stamp, i) => (i === index ? { ...stamp, ...patch } : stamp)));
  }

  onScopeChange(scope: EMergeStampScope | null): void {
    if (scope !== null) this.updateActiveStamp({ scope });
  }

  onRepeatModeChange(repeatMode: EMergeStampRepeatMode | null): void {
    if (repeatMode !== null) this.updateActiveStamp({ repeatMode });
  }

  removeStamp(index: number): void {
    this.stamps.update((list) => list.filter((_, i) => i !== index));
    this.activeIndex.set(-1);
    this.activePage.set(null);
  }

  clearStamps(): void {
    this.confirmDelete({ header: 'Xoá khối ảnh', message: 'Xoá tất cả khối dấu đỏ và chữ ký đã đặt?' }, () => {
      this.stamps.set([]);
      this.activeIndex.set(-1);
    });
  }

  resetStampSize(): void {
    const stamp = this.activeStamp();
    const page = this.activeCopyPage();
    const metric = page !== null ? this.pageMetrics().find((p) => p.number === page) : null;
    if (!stamp || page === null || !metric) return;

    const area = this.areaOf(stamp, page);
    const base = this.baseSize(stamp.kind, metric);
    this.applyArea(this.activeIndex(), page, this.writesToPage(stamp, page), {
      xRatio: Math.min(area.xRatio, 1 - base.width),
      yRatio: Math.min(area.yRatio, 1 - base.height),
      widthRatio: base.width,
      heightRatio: base.height
    });
  }

  onPaletteDragStart(event: DragEvent, kind: EMergeStampKind): void {
    if (!this.canPlace()) {
      event.preventDefault();
      return;
    }
    event.dataTransfer?.setData('text/plain', String(kind));
    this.paletteKind = kind;

    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    this.paletteGrabX = rect.width > 0 ? (event.clientX - rect.left) / rect.width : 0;
    this.paletteGrabY = rect.height > 0 ? (event.clientY - rect.top) / rect.height : 0;
  }

  onPageDragOver(event: DragEvent): void {
    event.preventDefault();
  }

  onPageDrop(event: DragEvent, page: IPdfPage): void {
    event.preventDefault();
    if (!this.canPlace()) return;

    const kind = this.paletteKind;
    const size = this.baseSize(kind, page);
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const x = (event.clientX - rect.left) / rect.width - this.paletteGrabX * size.width;
    const y = (event.clientY - rect.top) / rect.height - this.paletteGrabY * size.height;

    this.stamps.update((list) => [
      ...list,
      {
        kind,
        scope: EMergeStampScope.CurrentPage,
        repeatMode: EMergeStampRepeatMode.All,
        customPages: null,
        pageNumber: page.number,
        xRatio: Utils.clamp(x, 0, 1 - size.width),
        yRatio: Utils.clamp(y, 0, 1 - size.height),
        widthRatio: size.width,
        heightRatio: size.height,
        pagePositions: []
      }
    ]);
    this.activeIndex.set(this.stamps().length - 1);
    this.activePage.set(page.number);
  }

  onStampPointerDown(event: PointerEvent, item: IStampItem, mode: DragMode): void {
    event.preventDefault();
    event.stopPropagation();
    this.activeIndex.set(item.index);
    this.activePage.set(item.page);

    const host = (event.currentTarget as HTMLElement).closest('.stamp-layer') as HTMLElement | null;
    if (!host) return;

    const rect = host.getBoundingClientRect();
    this.drag = {
      mode,
      index: item.index,
      page: item.page,
      toPage: this.writesToPage(item.stamp, item.page),
      host,
      grabX: (event.clientX - rect.left) / rect.width - item.area.xRatio,
      grabY: (event.clientY - rect.top) / rect.height - item.area.yRatio
    };
    (event.target as HTMLElement).setPointerCapture(event.pointerId);
  }

  onStampPointerMove(event: PointerEvent): void {
    const drag = this.drag;
    const stamp = drag ? this.stamps()[drag.index] : null;
    const metric = drag ? this.pageMetrics().find((p) => p.number === drag.page) : null;
    if (!drag || !stamp || !metric) return;

    const current = drag.toPage ? this.areaOf(stamp, drag.page) : stamp;
    const rect = drag.host.getBoundingClientRect();
    const pointerX = (event.clientX - rect.left) / rect.width;
    const pointerY = (event.clientY - rect.top) / rect.height;

    let width = current.widthRatio;
    let height = current.heightRatio;
    let left = pointerX - drag.grabX;
    let top = pointerY - drag.grabY;

    if (drag.mode !== 'move') {
      const right = current.xRatio + current.widthRatio;
      const bottom = current.yRatio + current.heightRatio;
      const fromRight = drag.mode === 'ne' || drag.mode === 'se';
      const fromBottom = drag.mode === 'sw' || drag.mode === 'se';
      const draggedWidth = fromRight ? pointerX - current.xRatio : right - pointerX;

      const base = this.baseSize(stamp.kind, metric);
      const scale = Utils.clamp(draggedWidth / base.width, SIGN_BLOCK_MIN_SCALE, SIGN_BLOCK_MAX_SCALE);
      width = Math.min(base.width * scale, 1);
      height = Math.min(base.height * scale, 1);
      left = fromRight ? current.xRatio : right - width;
      top = fromBottom ? current.yRatio : bottom - height;
    }

    this.applyArea(drag.index, drag.page, drag.toPage, {
      xRatio: Utils.clamp(left, 0, 1 - width),
      yRatio: Utils.clamp(top, 0, 1 - height),
      widthRatio: width,
      heightRatio: height
    });
  }

  onStampPointerUp(event: PointerEvent): void {
    const target = event.target as HTMLElement;
    if (target.hasPointerCapture(event.pointerId)) target.releasePointerCapture(event.pointerId);
    this.drag = null;
  }
}
