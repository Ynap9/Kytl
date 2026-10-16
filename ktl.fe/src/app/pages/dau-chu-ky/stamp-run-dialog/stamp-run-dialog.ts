import { Component, OnDestroy, computed, effect, inject, input, model, signal, untracked } from '@angular/core';
import { Subscription } from 'rxjs';
import { ProgressBarModule } from 'primeng/progressbar';

import { SharedImports } from '../../../shared/import.shared';
import { BaseComponent } from '../../../shared/components/base/base-component';
import { IBaseResponse } from '../../../shared/models/request-paging.base.models';
import { DesktopService } from '../../../services/desktop.service';
import { PdfStampService } from '../../../services/pdf-stamp.service';
import { SignHandoffService } from '../../../services/sign-handoff.service';
import { IPdfStampProgress, IPdfStampRequest, IPdfStampResult, IPdfStampRunRow } from '../../../models/pdf-stamp.models';
import { PDF_STAMP_PROGRESS_TARGET, PDF_STAMP_ROW_STATUS_LABELS } from '../../../shared/constants/pdf-stamp.constants';

@Component({
  selector: 'app-stamp-run-dialog',
  imports: [...SharedImports, ProgressBarModule],
  templateUrl: './stamp-run-dialog.html'
})
export class StampRunDialog extends BaseComponent implements OnDestroy {
  private pdfStampService = inject(PdfStampService);
  private desktop = inject(DesktopService);
  private signHandoff = inject(SignHandoffService);

  visible = model<boolean>(false);
  request = input<IPdfStampRequest | null>(null);
  signAfter = input<boolean>(false);

  protected readonly statusLabels = PDF_STAMP_ROW_STATUS_LABELS;

  running = signal<boolean>(false);
  rows = signal<IPdfStampRunRow[]>([]);
  percent = signal<number>(0);
  resultFolder = signal<string>('');
  result = signal<IPdfStampResult | null>(null);

  doneCount = computed(() => this.rows().filter((row) => row.status === 'done').length);
  failedCount = computed(() => this.rows().filter((row) => row.status === 'failed').length);
  signPaths = computed(() =>
    this.rows()
      .filter((row) => row.status === 'done' && !!row.outputPath)
      .map((row) => row.outputPath as string)
  );

  private sub?: Subscription;

  private openEffect = effect(() => {
    const visible = this.visible();
    const request = this.request();
    untracked(() => {
      if (visible && request && !this.running()) this.start(request);
    });
  });

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  stop(): void {
    this.sub?.unsubscribe();
    this.running.set(false);
    this.rows.update((rows) => rows.map((row) => (row.status === 'processing' ? { ...row, status: 'idle' } : row)));
    this.messageWarning('Đã dừng. Các file đã xong vẫn được giữ lại.');
  }

  close(): void {
    if (!this.running()) this.visible.set(false);
  }

  goToSign(): void {
    this.visible.set(false);
    this.signHandoff.open(this.signPaths());
  }

  openPath(path: string | null | undefined): void {
    if (!path) return;
    if (this.desktop.openPath(path)) return;
    navigator.clipboard?.writeText(path).then(
      () => this.messageSuccess('Đã sao chép đường dẫn vào clipboard.'),
      () => this.messageWarning('Không mở được. Đường dẫn: ' + path)
    );
  }

  private start(request: IPdfStampRequest): void {
    this.running.set(true);
    this.rows.set([]);
    this.percent.set(0);
    this.result.set(null);
    this.resultFolder.set('');

    this.sub = this.pdfStampService.stamp(request).subscribe({
      next: (ev) => this.handleProgress(ev),
      error: (err: IBaseResponse | Error) => {
        this.running.set(false);
        this.visible.set(false);
        if ('status' in err) {
          this.isResponseSucceed(err);
          return;
        }
        this.messageError('Không kết nối được tới dịch vụ dấu & chữ ký. Vui lòng thử lại sau.');
      },
      complete: () => this.running.set(false)
    });
  }

  private handleProgress(ev: IPdfStampProgress): void {
    if (typeof ev.percent === 'number') this.percent.set(ev.percent);

    if (ev.targetType === PDF_STAMP_PROGRESS_TARGET.manifest) {
      this.resultFolder.set(ev.target);
      this.rows.set((ev.files ?? []).map((fileName, i) => ({ fileName, status: i === 0 ? 'processing' : 'idle', warnings: [] })));
    } else if (ev.targetType === PDF_STAMP_PROGRESS_TARGET.file && ev.file) {
      const file = ev.file;
      this.rows.update((rows) => {
        const updated = rows.map((row): IPdfStampRunRow =>
          row.fileName === file.fileName
            ? {
                ...row,
                status: file.success ? 'done' : 'failed',
                message: file.success ? null : file.reason,
                outputPath: file.outputPath,
                warnings: file.warnings ?? []
              }
            : row
        );
        const next = updated.findIndex((row) => row.status === 'idle');
        return next < 0 ? updated : updated.map((row, i) => (i === next ? { ...row, status: 'processing' } : row));
      });
    } else if (ev.targetType === PDF_STAMP_PROGRESS_TARGET.summary && ev.summary) {
      this.percent.set(100);
      this.resultFolder.set(ev.summary.outputFolder);
      this.result.set(ev.summary);
      if (this.signAfter() && ev.summary.failedCount === 0) this.goToSign();
    }
  }
}
