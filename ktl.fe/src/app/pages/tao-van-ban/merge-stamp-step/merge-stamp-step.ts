import { Component, computed, effect, inject, input, model, signal, untracked } from '@angular/core';

import { SharedImports } from '../../../shared/import.shared';
import { BaseComponent } from '../../../shared/components/base/base-component';
import { StampDesigner } from '../../../shared/components/stamp-designer/stamp-designer';
import { DocumentMergeService } from '../../../services/document-merge.service';
import {
  IMergePreviewRequest,
  IMergeRequest,
  IMergeRowCheck,
  IMergeStamp
} from '../../../models/document-merge.models';
import { EMergePreviewMode } from '../../../shared/constants/document-merge.constants';

@Component({
  selector: 'app-merge-stamp-step',
  imports: [...SharedImports, StampDesigner],
  templateUrl: './merge-stamp-step.html',
  host: { class: 'flex min-h-0 flex-1 flex-col' }
})
export class MergeStampStep extends BaseComponent {
  private documentMergeService = inject(DocumentMergeService);

  request = input.required<IMergeRequest>();
  rows = input<IMergeRowCheck[]>([]);
  enabled = model<boolean>(false);
  sealImagePath = model<string>('');
  wetSignatureImagePath = model<string>('');
  stamps = model<IMergeStamp[]>([]);

  rowNumber = signal<number | null>(null);
  mode = signal<EMergePreviewMode>(EMergePreviewMode.Edit);
  previewUrl = signal<string | null>(null);
  previewLoading = signal<boolean>(false);
  previewWarnings = signal<string[]>([]);

  rowOptions = computed(() =>
    this.rows().map((row) => ({ label: `Dòng ${row.rowNumber} · ${row.fileName}`, value: row.rowNumber }))
  );
  rowIndex = computed(() => this.rows().findIndex((row) => row.rowNumber === this.rowNumber()));

  private editPreviews = new Map<number, string>();
  private previewVersion = 0;

  private rowEffect = effect(() => {
    const rows = this.rows();
    untracked(() => {
      if (!rows.some((row) => row.rowNumber === this.rowNumber())) {
        this.rowNumber.set(rows[0]?.rowNumber ?? null);
      }
    });
  });

  private previewEffect = effect(() => {
    const rowNumber = this.rowNumber();
    const mode = this.mode();
    untracked(() => {
      if (rowNumber !== null) this.loadPreview(rowNumber, mode);
    });
  });

  goToRow(offset: number): void {
    const row = this.rows()[this.rowIndex() + offset];
    if (row) this.rowNumber.set(row.rowNumber);
  }

  refreshPreview(): void {
    const rowNumber = this.rowNumber();
    if (rowNumber === null) return;
    this.editPreviews.delete(rowNumber);
    this.loadPreview(rowNumber, this.mode());
  }

  private loadPreview(rowNumber: number, mode: EMergePreviewMode): void {
    const cached = mode === EMergePreviewMode.Edit ? this.editPreviews.get(rowNumber) : undefined;
    if (cached) {
      this.previewVersion++;
      this.previewLoading.set(false);
      this.previewWarnings.set([]);
      this.previewUrl.set(cached);
      return;
    }

    const final = mode === EMergePreviewMode.Final;
    const request: IMergePreviewRequest = {
      ...this.request(),
      rowNumber,
      sealImagePath: final ? this.sealImagePath() || null : null,
      wetSignatureImagePath: final ? this.wetSignatureImagePath() || null : null,
      stamps: final ? this.stamps() : []
    };

    const version = ++this.previewVersion;
    this.previewLoading.set(true);
    this.documentMergeService.preview(request).subscribe({
      next: (res) => {
        if (version !== this.previewVersion) return;
        this.previewLoading.set(false);
        if (!this.isResponseSucceed(res)) {
          if (final) this.mode.set(EMergePreviewMode.Edit);
          return;
        }
        const url = this.documentMergeService.previewFileUrl(res.data.filePath);
        if (!final) this.editPreviews.set(rowNumber, url);
        this.previewWarnings.set(res.data.warnings ?? []);
        this.previewUrl.set(url);
      },
      error: () => {
        if (version !== this.previewVersion) return;
        this.previewLoading.set(false);
        this.messageError('Không tạo được bản xem trước. Vui lòng thử lại sau.');
      }
    });
  }
}
