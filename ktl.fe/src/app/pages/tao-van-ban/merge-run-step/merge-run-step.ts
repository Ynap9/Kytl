import { Component, OnDestroy, computed, inject, input, model, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { ProgressBarModule } from 'primeng/progressbar';

import { SharedImports } from '../../../shared/import.shared';
import { BaseComponent } from '../../../shared/components/base/base-component';
import { IBaseResponse } from '../../../shared/models/request-paging.base.models';
import { DesktopService } from '../../../services/desktop.service';
import { DocumentMergeService } from '../../../services/document-merge.service';
import { SignHandoffService } from '../../../services/sign-handoff.service';
import {
  IMergeProgress,
  IMergeRequest,
  IMergeResult,
  IMergeRunRow,
  IMergeValidation
} from '../../../models/document-merge.models';
import { MERGE_PROGRESS_TARGET, MERGE_ROW_STATUS_LABELS } from '../../../shared/constants/document-merge.constants';
import { PathPicker } from '../../../shared/components/path-picker/path-picker';
import { MergeResultDialog } from '../merge-result-dialog/merge-result-dialog';

@Component({
  selector: 'app-merge-run-step',
  imports: [...SharedImports, ProgressBarModule, PathPicker, MergeResultDialog],
  templateUrl: './merge-run-step.html',
  styleUrl: './merge-run-step.scss',
  host: { class: 'flex min-h-0 flex-1 flex-col gap-4' }
})
export class MergeRunStep extends BaseComponent implements OnDestroy {
  private documentMergeService = inject(DocumentMergeService);
  private desktop = inject(DesktopService);
  private signHandoff = inject(SignHandoffService);

  request = input.required<IMergeRequest>();
  templateName = input<string>('');
  fileNameHeader = input<string | null>(null);
  outputFolder = model<string>('');
  running = model<boolean>(false);
  signAfter = model<boolean>(false);

  protected readonly statusLabels = MERGE_ROW_STATUS_LABELS;

  validating = signal<boolean>(false);
  validation = signal<IMergeValidation | null>(null);
  rows = signal<IMergeRunRow[]>([]);
  percent = signal<number>(0);
  resultFolder = signal<string>('');
  finished = signal<boolean>(false);
  result = signal<IMergeResult | null>(null);
  showResult = signal<boolean>(false);

  totalFields = computed(() => {
    const validation = this.validation();
    return validation ? validation.mappedFieldCount + validation.unmappedFields.length : 0;
  });
  doneCount = computed(() => this.rows().filter((row) => row.status === 'done').length);
  failedCount = computed(() => this.rows().filter((row) => row.status === 'failed').length);
  signPaths = computed(() =>
    this.rows()
      .filter((row) => row.status === 'done' && !!row.outputPath)
      .map((row) => row.outputPath as string)
  );
  canStart = computed(() => !!this.validation() && this.rows().length > 0 && !this.validating() && !this.running());

  private sub?: Subscription;

  override ngOnInit(): void {
    this.validate();
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  validate(): void {
    if (this.running()) return;
    this.validating.set(true);
    this.finished.set(false);
    this.percent.set(0);
    this.documentMergeService.validate(this.request()).subscribe({
      next: (res) => {
        this.validating.set(false);
        if (!this.isResponseSucceed(res)) {
          this.validation.set(null);
          this.rows.set([]);
          return;
        }
        this.validation.set(res.data);
        this.rows.set(res.data.rows.map((row) => ({ ...row, status: 'idle' })));
      },
      error: () => {
        this.validating.set(false);
        this.messageError('Không kết nối được tới dịch vụ tạo văn bản. Vui lòng thử lại sau.');
      }
    });
  }

  start(): void {
    if (!this.canStart()) return;

    this.running.set(true);
    this.finished.set(false);
    this.percent.set(0);
    this.result.set(null);
    this.resultFolder.set('');
    this.rows.update((rows) => rows.map((row) => ({ ...row, status: 'idle', message: null, outputPath: null })));

    this.sub = this.documentMergeService.merge(this.request()).subscribe({
      next: (ev) => this.handleProgress(ev),
      error: (err: IBaseResponse | Error) => {
        this.running.set(false);
        this.resetProcessingRows();
        if ('status' in err) {
          this.isResponseSucceed(err);
          return;
        }
        this.messageError('Không kết nối được tới dịch vụ tạo văn bản. Vui lòng thử lại sau.');
      },
      complete: () => {
        this.running.set(false);
        this.finished.set(true);
      }
    });
  }

  stop(): void {
    this.confirmAction(
      { header: 'Dừng tạo file', message: 'Dừng tiến trình? Các file đã tạo xong vẫn được giữ lại.' },
      () => {
        this.sub?.unsubscribe();
        this.running.set(false);
        this.finished.set(true);
        this.resetProcessingRows();
        this.messageWarning('Đã dừng tạo file.');
      }
    );
  }

  goToSign(): void {
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

  private handleProgress(ev: IMergeProgress): void {
    if (typeof ev.percent === 'number') this.percent.set(ev.percent);

    if (ev.targetType === MERGE_PROGRESS_TARGET.manifest) {
      this.resultFolder.set(ev.target);
      this.markNextProcessing();
    } else if (ev.targetType === MERGE_PROGRESS_TARGET.file && ev.file) {
      const file = ev.file;
      this.rows.update((rows) =>
        rows.map((row) =>
          row.rowNumber === file.rowNumber
            ? {
                ...row,
                status: file.success ? 'done' : 'failed',
                message: file.success ? null : file.reason,
                outputPath: file.outputPath,
                warnings: [...new Set([...row.warnings, ...(file.warnings ?? [])])]
              }
            : row
        )
      );
      this.markNextProcessing();
    } else if (ev.targetType === MERGE_PROGRESS_TARGET.summary && ev.summary) {
      this.percent.set(100);
      this.resultFolder.set(ev.summary.outputFolder);
      this.result.set(ev.summary);
      if (this.signAfter() && ev.summary.failedCount === 0) {
        this.goToSign();
        return;
      }
      this.showResult.set(true);
    }
  }

  private markNextProcessing(): void {
    const next = this.rows().find((row) => row.status === 'idle');
    if (!next) return;
    this.rows.update((rows) =>
      rows.map((row) => (row.rowNumber === next.rowNumber ? { ...row, status: 'processing' } : row))
    );
  }

  private resetProcessingRows(): void {
    this.rows.update((rows) => rows.map((row) => (row.status === 'processing' ? { ...row, status: 'idle' } : row)));
  }
}
