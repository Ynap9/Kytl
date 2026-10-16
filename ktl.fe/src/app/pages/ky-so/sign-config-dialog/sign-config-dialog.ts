import {
  Component,
  computed,
  effect,
  inject,
  input,
  model,
  output,
  signal,
  untracked
} from '@angular/core';
import { ColorPickerModule } from 'primeng/colorpicker';
import { SliderModule } from 'primeng/slider';
import { ToggleSwitchModule } from 'primeng/toggleswitch';

import { SharedImports } from '../../../shared/import.shared';
import { BaseComponent } from '../../../shared/components/base/base-component';
import { StampDesigner } from '../../../shared/components/stamp-designer/stamp-designer';
import { Utils } from '../../../shared/utils';
import { SignService } from '../../../services/sign.service';
import { IMergeStamp } from '../../../models/document-merge.models';
import {
  ISignConfig,
  ISignFileItem,
  ISignLayout,
  ISignPosition
} from '../../../models/sign.models';
import { IPdfPage, IPdfPageMetric } from '../../../shared/models/pdf-pages.models';
import { EMergeStampKind } from '../../../shared/constants/document-merge.constants';
import {
  SIGN_DEFAULT_CONFIG,
  SIGN_FONT_FAMILIES,
  SIGN_FONT_SIZE_MAX,
  SIGN_FONT_SIZE_MIN,
  SIGN_FONT_SIZE_STEP,
  SIGN_IMAGE_DARKNESS_BRIGHTNESS_FACTOR,
  SIGN_IMAGE_DARKNESS_MAX,
  SIGN_IMAGE_DARKNESS_MIN,
  SIGN_IMAGE_DARKNESS_MIN_BRIGHTNESS,
  SIGN_IMAGE_STROKE_MAX,
  SIGN_IMAGE_STROKE_MIN,
  SIGN_LINE_HEIGHT_RATIO,
  SIGN_PAGE_MARGIN_PT,
  SIGN_REFERENCE_PAGE_HEIGHT_PT,
  SIGN_REFERENCE_PAGE_WIDTH_PT,
  SIGN_SIGNER_PLACEHOLDER,
  SIGN_TIME_PREVIEW_FORMAT
} from '../../../shared/constants/sign.constants';

interface ISignatureBox {
  xRatio: number;
  yRatio: number;
  widthRatio: number;
  heightRatio: number;
}

const REFERENCE_PAGE: IPdfPageMetric = {
  number: 1,
  widthPt: SIGN_REFERENCE_PAGE_WIDTH_PT,
  heightPt: SIGN_REFERENCE_PAGE_HEIGHT_PT
};

@Component({
  selector: 'app-sign-config-dialog',
  imports: [...SharedImports, ColorPickerModule, SliderModule, ToggleSwitchModule, StampDesigner],
  templateUrl: './sign-config-dialog.html',
  styleUrl: './sign-config-dialog.scss'
})
export class SignConfigDialog extends BaseComponent {
  private signService = inject(SignService);

  visible = model<boolean>(false);
  sources = input<string[]>([]);
  certName = input<string>('');
  config = input.required<ISignConfig>();
  save = output<ISignConfig>();

  protected readonly fontFamilies = SIGN_FONT_FAMILIES.map((font) => ({
    label: font,
    value: font
  }));
  protected readonly fontSizeMin = SIGN_FONT_SIZE_MIN;
  protected readonly fontSizeMax = SIGN_FONT_SIZE_MAX;
  protected readonly fontSizeStep = SIGN_FONT_SIZE_STEP;
  protected readonly darknessMin = SIGN_IMAGE_DARKNESS_MIN;
  protected readonly darknessMax = SIGN_IMAGE_DARKNESS_MAX;
  protected readonly strokeMin = SIGN_IMAGE_STROKE_MIN;
  protected readonly strokeMax = SIGN_IMAGE_STROKE_MAX;
  protected readonly lineHeightRatio = SIGN_LINE_HEIGHT_RATIO;
  protected readonly previewTime = Utils.formatDateView(new Date(), SIGN_TIME_PREVIEW_FORMAT);

  draft = signal<ISignConfig>({ ...SIGN_DEFAULT_CONFIG });
  files = signal<ISignFileItem[]>([]);
  loadingFiles = signal<boolean>(false);
  fileIndex = signal<number>(0);
  pageMetrics = signal<IPdfPageMetric[]>([]);
  signatureActive = signal<boolean>(false);

  currentFile = computed<ISignFileItem | null>(() => this.files()[this.fileIndex()] ?? null);
  fileOptions = computed(() =>
    this.files().map((file, index) => ({ label: file.name, value: index }))
  );
  previewUrl = computed<string | null>(() => {
    const file = this.currentFile();
    return file ? this.signService.pdfFileUrl(file.fullPath) : null;
  });

  ownLayout = computed<ISignLayout | null>(() => {
    const file = this.currentFile();
    if (!file) return null;
    return this.draft().fileOverrides.find((item) => item.path === file.fullPath) ?? null;
  });
  layout = computed<ISignLayout>(() => this.ownLayout() ?? this.draft());
  ownLayoutCount = computed(() => {
    const paths = this.files().map((file) => file.fullPath);
    return this.draft().fileOverrides.filter((item) => paths.includes(item.path)).length;
  });

  pageCount = computed(() => this.pageMetrics().length);
  signerLabel = computed(() => this.certName() || SIGN_SIGNER_PLACEHOLDER);
  signaturePage = computed(() => this.layout().signaturePosition?.pageNumber ?? 1);

  imageFilters = computed<Partial<Record<EMergeStampKind, string>>>(() => ({
    [EMergeStampKind.Seal]: this.darknessFilter(this.draft().sealDarkness),
    [EMergeStampKind.WetSignature]: this.darknessFilter(this.draft().wetSignatureDarkness)
  }));

  private measureContext = document.createElement('canvas').getContext('2d');
  private drag: { host: HTMLElement; page: IPdfPage; grabX: number; grabY: number } | null = null;

  private openEffect = effect(() => {
    if (!this.visible()) return;
    untracked(() => {
      this.draft.set(structuredClone({ ...SIGN_DEFAULT_CONFIG, ...this.config() }));
      this.signatureActive.set(false);
      this.loadFiles();
    });
  });

  patch(value: Partial<ISignConfig>): void {
    this.draft.update((draft) => ({ ...draft, ...value }));
  }

  onStampsChange(stamps: IMergeStamp[]): void {
    this.updateLayout({ stamps });
  }

  onSealImageChange(path: string): void {
    this.patch({ sealImagePath: path });
    if (!path) this.removeStampsOf(EMergeStampKind.Seal);
  }

  onWetSignatureImageChange(path: string): void {
    this.patch({ wetSignatureImagePath: path });
    if (!path) this.removeStampsOf(EMergeStampKind.WetSignature);
  }

  onPagesLoaded(metrics: IPdfPageMetric[]): void {
    this.pageMetrics.set(metrics);
  }

  selectFile(index: number): void {
    if (index < 0 || index >= this.files().length) return;
    this.pageMetrics.set([]);
    this.fileIndex.set(index);
  }

  toggleOwnLayout(enabled: boolean): void {
    const file = this.currentFile();
    if (!file) return;

    const others = this.draft().fileOverrides.filter((item) => item.path !== file.fullPath);
    if (!enabled) {
      this.patch({ fileOverrides: others });
      return;
    }

    const draft = this.draft();
    this.patch({
      fileOverrides: [
        ...others,
        {
          path: file.fullPath,
          signaturePosition: draft.signaturePosition ? { ...draft.signaturePosition } : null,
          stamps: structuredClone(draft.stamps)
        }
      ]
    });
  }

  setSignaturePage(pageNumber: number | null): void {
    if (!pageNumber) return;
    const page = Utils.clamp(pageNumber, 1, Math.max(this.pageCount(), 1));
    const metric = this.pageMetrics().find((item) => item.number === page) ?? REFERENCE_PAGE;
    const current = this.layout().signaturePosition;
    const size = this.signatureSize(metric);
    const box = current ?? this.defaultSignatureBox(metric);
    this.updateLayout({
      signaturePosition: {
        pageNumber: page,
        xRatio: Utils.clamp(box.xRatio, 0, 1 - size.widthRatio),
        yRatio: Utils.clamp(box.yRatio, 0, 1 - size.heightRatio),
        ...size
      }
    });
  }

  resetSignaturePosition(): void {
    this.updateLayout({ signaturePosition: null });
  }

  signatureBox(page: IPdfPage): ISignatureBox | null {
    if (!this.draft().showSignature) return null;

    const position = this.layout().signaturePosition;
    if (!position) return page.number === 1 ? this.defaultSignatureBox(page) : null;
    if (position.pageNumber !== page.number) return null;

    const size = this.signatureSize(page);
    const anchorRight = position.xRatio + position.widthRatio / 2 > 0.5;
    const left = anchorRight
      ? position.xRatio + position.widthRatio - size.widthRatio
      : position.xRatio;
    return {
      xRatio: Utils.clamp(left, 0, 1 - size.widthRatio),
      yRatio: Utils.clamp(position.yRatio, 0, 1 - size.heightRatio),
      ...size
    };
  }

  signatureFontPx(page: IPdfPage): number {
    return (this.pageFontPt(page) * page.cssWidth) / page.widthPt;
  }

  onSignaturePointerDown(event: PointerEvent, page: IPdfPage): void {
    const box = this.signatureBox(page);
    const host = (event.currentTarget as HTMLElement).closest('.stamp-layer') as HTMLElement | null;
    if (!box || !host) return;

    event.preventDefault();
    event.stopPropagation();
    this.signatureActive.set(true);

    const rect = host.getBoundingClientRect();
    this.drag = {
      host,
      page,
      grabX: (event.clientX - rect.left) / rect.width - box.xRatio,
      grabY: (event.clientY - rect.top) / rect.height - box.yRatio
    };
    (event.target as HTMLElement).setPointerCapture(event.pointerId);
  }

  onSignaturePointerMove(event: PointerEvent): void {
    const drag = this.drag;
    if (!drag) return;

    const rect = drag.host.getBoundingClientRect();
    const size = this.signatureSize(drag.page);
    const left = (event.clientX - rect.left) / rect.width - drag.grabX;
    const top = (event.clientY - rect.top) / rect.height - drag.grabY;
    this.updateLayout({
      signaturePosition: {
        pageNumber: drag.page.number,
        xRatio: Utils.clamp(left, 0, 1 - size.widthRatio),
        yRatio: Utils.clamp(top, 0, 1 - size.heightRatio),
        ...size
      }
    });
  }

  onSignaturePointerUp(event: PointerEvent): void {
    const target = event.target as HTMLElement;
    if (target.hasPointerCapture(event.pointerId)) target.releasePointerCapture(event.pointerId);
    this.drag = null;
  }

  onCancel(): void {
    this.visible.set(false);
  }

  onSave(): void {
    const draft = this.draft();
    this.save.emit({
      ...draft,
      reason: draft.reason.trim(),
      location: draft.location.trim(),
      signaturePosition: this.fitSignature(draft.signaturePosition),
      fileOverrides: draft.fileOverrides.map((item) => ({
        ...item,
        signaturePosition: this.fitSignature(item.signaturePosition)
      }))
    });
    this.visible.set(false);
  }

  private loadFiles(): void {
    this.files.set([]);
    this.fileIndex.set(0);
    this.pageMetrics.set([]);
    const sources = this.sources();
    if (sources.length === 0) return;

    this.loadingFiles.set(true);
    this.signService.getFiles(sources).subscribe({
      next: (res) => {
        this.loadingFiles.set(false);
        if (this.isResponseSucceed(res)) this.files.set(res.data.files);
      },
      error: () => {
        this.loadingFiles.set(false);
        this.messageError('Không tải được danh sách file để xem trước. Vui lòng thử lại sau.');
      }
    });
  }

  private updateLayout(value: Partial<ISignLayout>): void {
    const own = this.ownLayout();
    const file = this.currentFile();
    if (!own || !file) {
      this.patch(value);
      return;
    }

    this.patch({
      fileOverrides: this.draft().fileOverrides.map((item) =>
        item.path === file.fullPath ? { ...item, ...value } : item
      )
    });
  }

  private removeStampsOf(kind: EMergeStampKind): void {
    this.draft.update((draft) => ({
      ...draft,
      stamps: draft.stamps.filter((stamp) => stamp.kind !== kind),
      fileOverrides: draft.fileOverrides.map((item) => ({
        ...item,
        stamps: item.stamps.filter((stamp) => stamp.kind !== kind)
      }))
    }));
  }

  private fitSignature(position: ISignPosition | null): ISignPosition | null {
    if (!position) return null;
    const metric =
      this.pageMetrics().find((item) => item.number === position.pageNumber) ?? REFERENCE_PAGE;
    return { ...position, ...this.signatureSize(metric) };
  }

  private defaultSignatureBox(page: IPdfPageMetric): ISignatureBox {
    const size = this.signatureSize(page);
    const margin = SIGN_PAGE_MARGIN_PT * this.pageScale(page);
    return {
      xRatio: Math.max(1 - size.widthRatio - margin / page.widthPt, 0),
      yRatio: Math.min(margin / page.heightPt, 1 - size.heightRatio),
      ...size
    };
  }

  private signatureSize(page: IPdfPageMetric): { widthRatio: number; heightRatio: number } {
    const fontPt = this.pageFontPt(page);
    let textWidth = 0;
    if (this.measureContext) {
      this.measureContext.font = `${fontPt}px "${this.draft().fontFamily}"`;
      textWidth = Math.max(
        this.measureContext.measureText(this.signerLabel()).width,
        this.measureContext.measureText(this.previewTime).width
      );
    }
    return {
      widthRatio: Math.min(textWidth / page.widthPt, 1),
      heightRatio: Math.min((fontPt * SIGN_LINE_HEIGHT_RATIO * 2) / page.heightPt, 1)
    };
  }

  private pageFontPt(page: IPdfPageMetric): number {
    return this.draft().fontSize * this.pageScale(page);
  }

  private pageScale(page: IPdfPageMetric): number {
    return Math.min(
      page.widthPt / SIGN_REFERENCE_PAGE_WIDTH_PT,
      page.heightPt / SIGN_REFERENCE_PAGE_HEIGHT_PT
    );
  }

  private darknessFilter(darkness: number): string {
    const factor = darkness / 100;
    const brightness = Math.max(
      SIGN_IMAGE_DARKNESS_MIN_BRIGHTNESS,
      1 - (factor - 1) * SIGN_IMAGE_DARKNESS_BRIGHTNESS_FACTOR
    );
    return `contrast(${factor}) brightness(${brightness})`;
  }
}
