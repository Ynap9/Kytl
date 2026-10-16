import { Component, computed, effect, inject, signal, untracked } from '@angular/core';

import { SharedImports } from '../../shared/import.shared';
import { BaseComponent } from '../../shared/components/base/base-component';
import { PdfSourcePicker } from '../../shared/components/pdf-source-picker/pdf-source-picker';
import { StampDesigner } from '../../shared/components/stamp-designer/stamp-designer';
import { PdfStampService } from '../../services/pdf-stamp.service';
import { IMergeStamp } from '../../models/document-merge.models';
import { IPdfStampFile, IPdfStampRequest } from '../../models/pdf-stamp.models';
import { EMergePreviewMode } from '../../shared/constants/document-merge.constants';
import { PDF_STAMP_PAGE_TITLE } from '../../shared/constants/pdf-stamp.constants';
import { StampOutputDialog } from './stamp-output-dialog/stamp-output-dialog';
import { StampRunDialog } from './stamp-run-dialog/stamp-run-dialog';

@Component({
  selector: 'app-dau-chu-ky',
  imports: [...SharedImports, PdfSourcePicker, StampDesigner, StampOutputDialog, StampRunDialog],
  templateUrl: './dau-chu-ky.html',
  styleUrl: './dau-chu-ky.scss'
})
export class DauChuKy extends BaseComponent {
  private pdfStampService = inject(PdfStampService);

  protected readonly title = PDF_STAMP_PAGE_TITLE;

  sources = signal<string[]>([]);
  droppedSources = signal<string[]>([]);
  outputFolder = signal<string>('');
  signAfter = signal<boolean>(false);

  files = signal<IPdfStampFile[]>([]);
  loadingFiles = signal<boolean>(false);
  fileIndex = signal<number>(0);

  sealImagePath = signal<string>('');
  wetSignatureImagePath = signal<string>('');
  stamps = signal<IMergeStamp[]>([]);
  mode = signal<EMergePreviewMode>(EMergePreviewMode.Edit);

  previewUrl = signal<string | null>(null);
  previewLoading = signal<boolean>(false);
  previewWarnings = signal<string[]>([]);

  showOutput = signal<boolean>(false);
  showRun = signal<boolean>(false);
  runRequest = signal<IPdfStampRequest | null>(null);

  currentFile = computed<IPdfStampFile | null>(() => this.files()[this.fileIndex()] ?? null);
  fileOptions = computed(() => this.files().map((file, index) => ({ label: file.name, value: index })));
  signedCount = computed(() => this.files().filter((file) => file.alreadySigned).length);
  unreadableCount = computed(() => this.files().filter((file) => !file.readable).length);
  hasDropped = computed(() => this.sources().some((source) => this.droppedSources().includes(source)));
  canStamp = computed(() => this.files().length > 0 && this.stamps().length > 0 && !this.loadingFiles());
  stampHint = computed(() => {
    if (this.files().length === 0) return 'Chọn file hoặc thư mục PDF trước';
    if (this.stamps().length === 0) return 'Kéo ảnh dấu hoặc chữ ký thả vào trang trước';
    return '';
  });

  private previewVersion = 0;

  private sourceEffect = effect(() => {
    const sources = this.sources();
    untracked(() => this.loadFiles(sources));
  });

  private previewEffect = effect(() => {
    const file = this.currentFile();
    const mode = this.mode();
    untracked(() => this.loadPreview(file, mode));
  });

  selectFile(index: number): void {
    this.fileIndex.set(index);
  }

  reloadFiles(): void {
    this.loadFiles(this.sources());
  }

  goToFile(offset: number): void {
    const index = this.fileIndex() + offset;
    if (index >= 0 && index < this.files().length) this.fileIndex.set(index);
  }

  startStamp(): void {
    if (this.canStamp()) this.showOutput.set(true);
  }

  runStamp(): void {
    this.runRequest.set({
      sourcePaths: this.sources(),
      outputFolder: this.outputFolder() || null,
      sealImagePath: this.sealImagePath() || null,
      wetSignatureImagePath: this.wetSignatureImagePath() || null,
      stamps: this.stamps()
    });
    this.showRun.set(true);
  }

  private loadFiles(sources: string[]): void {
    this.fileIndex.set(0);
    if (sources.length === 0) {
      this.files.set([]);
      return;
    }

    this.loadingFiles.set(true);
    this.pdfStampService.getFiles(sources).subscribe({
      next: (res) => {
        this.loadingFiles.set(false);
        if (!this.isResponseSucceed(res)) {
          this.files.set([]);
          return;
        }
        this.files.set(res.data.files);
        if (res.data.files.length === 0) {
          this.messageWarning('Nguồn đã chọn không có file PDF nào.');
        }
      },
      error: () => {
        this.loadingFiles.set(false);
        this.files.set([]);
        this.messageError('Không kết nối được tới dịch vụ dấu & chữ ký. Vui lòng thử lại sau.');
      }
    });
  }

  private loadPreview(file: IPdfStampFile | null, mode: EMergePreviewMode): void {
    const version = ++this.previewVersion;
    this.previewWarnings.set([]);
    if (!file) {
      this.previewLoading.set(false);
      this.previewUrl.set(null);
      return;
    }

    if (mode === EMergePreviewMode.Edit) {
      this.previewLoading.set(false);
      this.previewUrl.set(this.pdfStampService.fileUrl(file.fullPath));
      return;
    }

    this.previewLoading.set(true);
    this.pdfStampService
      .preview({
        filePath: file.fullPath,
        sealImagePath: this.sealImagePath() || null,
        wetSignatureImagePath: this.wetSignatureImagePath() || null,
        stamps: this.stamps()
      })
      .subscribe({
        next: (res) => {
          if (version !== this.previewVersion) return;
          this.previewLoading.set(false);
          if (!this.isResponseSucceed(res)) {
            this.mode.set(EMergePreviewMode.Edit);
            return;
          }
          this.previewWarnings.set(res.data.warnings ?? []);
          this.previewUrl.set(this.pdfStampService.fileUrl(res.data.filePath));
        },
        error: () => {
          if (version !== this.previewVersion) return;
          this.previewLoading.set(false);
          this.messageError('Không tạo được bản xem trước. Vui lòng thử lại sau.');
        }
      });
  }
}
