import { Component, OnDestroy, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Subscription } from 'rxjs';
import { ProgressBarModule } from 'primeng/progressbar';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import { SharedImports } from '../../../shared/import.shared';
import { BaseComponent } from '../../../shared/components/base/base-component';
import { Utils } from '../../../shared/utils';
import { DesktopService } from '../../../services/desktop.service';
import { SignService } from '../../../services/sign.service';
import { SignCertStateService } from '../../../services/sign-cert-state.service';
import {
  ISignConfig,
  ISignFileRow,
  ISignPositionCheck,
  ISignProgress,
  ISignRequest,
  ISignResult
} from '../../../models/sign.models';
import { IBaseResponse } from '../../../shared/models/request-paging.base.models';
import { SIGN_DEFAULT_CONFIG } from '../../../shared/constants/sign.constants';
import { SignDialog } from './sign-dialog/sign-dialog';
import { SignResultDialog } from './sign-result-dialog/sign-result-dialog';

@Component({
  selector: 'app-sign-step',
  imports: [...SharedImports, ProgressBarModule, TableModule, TagModule, SignDialog, SignResultDialog],
  templateUrl: './sign-step.html',
  styleUrl: './sign-step.scss'
})
export class SignStep extends BaseComponent implements OnDestroy {
  private signService = inject(SignService);
  private certState = inject(SignCertStateService);
  protected desktop = inject(DesktopService);

  thumbprint = input<string>('');
  certName = input<string>('');
  sources = input<string[]>([]);
  config = input<ISignConfig | null>(null);
  reason = input<string>('');
  location = input<string>('');

  private checkedSources: string[] = [];

  constructor() {
    super();
    effect(() => {
      const sources = this.sources();
      this.config();
      if (sources.length === 0) return;

      untracked(() => {
        if (sources.join('|') !== this.checkedSources.join('|')) {
          this.startCheck();
          return;
        }
        this.checkPositions(sources);
      });
    });
  }

  checking = signal<boolean>(false);
  signing = signal<boolean>(false);
  verifying = signal<boolean>(false);
  checkDone = signal<boolean>(false);
  percent = signal<number>(0);

  positionChecking = signal<boolean>(false);
  positionCheck = signal<ISignPositionCheck | null>(null);

  fileRows = signal<ISignFileRow[]>([]);
  showSignDialog = signal<boolean>(false);
  showResultDialog = signal<boolean>(false);
  signResult = signal<ISignResult | null>(null);

  adjustedFiles = computed(() => (this.positionCheck()?.files ?? []).filter((f) => !f.fitted));

  sortedFileRows = computed(() =>
    [...this.fileRows()].sort((a, b) => Utils.compareDocSeq(a.name, b.name))
  );

  private sub?: Subscription;

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  canCheck(): boolean {
    return this.sources().length > 0 && !this.checking() && !this.signing();
  }

  canSign(): boolean {
    return (
      !!this.thumbprint() &&
      this.checkDone() &&
      this.fileRows().length > 0 &&
      !this.checking() &&
      !this.signing() &&
      !this.verifying()
    );
  }

  startCheck(): void {
    const sources = this.sources();
    if (sources.length === 0) return;

    this.checkedSources = sources;
    this.checkPositions(sources);
    this.sub?.unsubscribe();
    this.checking.set(true);
    this.checkDone.set(false);
    this.percent.set(0);
    this.fileRows.set([]);

    this.sub = this.signService.getFiles(sources).subscribe({
      next: (res) => {
        this.checking.set(false);
        if (!this.isResponseSucceed(res)) return;
        this.fileRows.set(res.data.files.map((f) => ({ name: f.name, signStatus: 'idle' as const })));
        this.checkDone.set(true);
      },
      error: () => {
        this.checking.set(false);
        this.messageError('Không kết nối được tới dịch vụ ký số. Vui lòng thử lại sau.');
      }
    });
  }

  private checkPositions(sources: string[]): void {
    const config = this.config() ?? SIGN_DEFAULT_CONFIG;

    this.positionChecking.set(true);
    this.positionCheck.set(null);
    this.signService
      .checkPositions({
        sourcePaths: sources,
        useCustomPosition: config.useCustomPosition,
        positions: config.positions ?? null,
        fileOverrides: config.fileOverrides ?? null
      })
      .subscribe({
        next: (res) => {
          this.positionChecking.set(false);
          if (!this.isResponseSucceed(res)) {
            this.messageError(res.message || 'Không kiểm được vị trí chữ ký.');
            return;
          }
          this.positionCheck.set(res.data);
        },
        error: () => {
          this.positionChecking.set(false);
          this.messageError('Không kết nối được tới dịch vụ kiểm tra vị trí chữ ký. Vui lòng thử lại sau.');
        }
      });
  }

  onOpenSignDialog(): void {
    if (!this.canSign()) {
      this.messageWarning('Không có file PDF nào để ký. Vui lòng kiểm tra lại.');
      return;
    }
    if (this.certState.verifiedCert()?.thumbprint === this.thumbprint()) {
      this.showSignDialog.set(true);
      return;
    }

    this.verifying.set(true);
    this.signService.verifyToken(this.thumbprint()).subscribe({
      next: (res) => {
        this.verifying.set(false);
        if (!this.isResponseSucceed(res)) return;
        if (res.data?.valid) {
          this.certState.verifiedCert.set(res.data);
          this.showSignDialog.set(true);
        } else {
          this.certState.verifiedCert.set(null);
          this.messageError(res.data?.reason || 'Chứng thư số chưa sẵn sàng để ký. Vui lòng kiểm tra lại token/PIN.');
        }
      },
      error: () => {
        this.verifying.set(false);
        this.messageError('Không kiểm tra được chứng thư số. Vui lòng thử lại sau.');
      }
    });
  }

  onStartSign(payload: { reason: string; location: string }): void {
    const sources = this.sources();
    if (sources.length === 0 || !this.thumbprint()) return;

    const config = this.config() ?? SIGN_DEFAULT_CONFIG;

    const req: ISignRequest = {
      thumbprint: this.thumbprint(),
      sourcePaths: sources,
      reason: payload.reason || undefined,
      location: payload.location || undefined,
      useCustomPosition: config.useCustomPosition,
      allowOverlapContent: config.allowOverlapContent,
      positions: config.positions ?? null,
      fileOverrides: config.fileOverrides ?? null,
      fontFamily: this.config()?.fontFamily ?? null,
      fontSize: this.config()?.fontSize ?? null,
      textColor: this.config()?.textColor ?? null,
      sealImagePath: this.config()?.sealImagePath ?? null,
      wetSignatureImagePath: this.config()?.wetSignatureImagePath ?? null
    };

    this.sub?.unsubscribe();
    this.signing.set(true);
    this.percent.set(0);
    this.fileRows.update((rows) =>
      rows.map((r) => ({
        ...r,
        signStatus: 'processing' as const,
        signMessage: undefined,
        signedTime: undefined,
        trustedTime: undefined
      }))
    );

    this.sub = this.signService.signFolder(req).subscribe({
      next: (ev) => this.handleSignEvent(ev),
      error: (err: IBaseResponse | Error) => {
        this.signing.set(false);
        this.fileRows.update((rows) =>
          rows.map((r) => (r.signStatus === 'processing' ? { ...r, signStatus: 'idle' as const } : r))
        );
        if ('status' in err) {
          this.isResponseSucceed(err);
          return;
        }
        this.messageError('Không kết nối được tới dịch vụ ký số. Vui lòng thử lại sau.');
      },
      complete: () => this.signing.set(false)
    });
  }

  private handleSignEvent(ev: ISignProgress): void {
    if (typeof ev.percent === 'number') {
      this.percent.set(ev.percent);
    }

    if (ev.targetType === 'file' && ev.file) {
      const f = ev.file;
      this.fileRows.update((rows) =>
        rows.map((r) =>
          r.name === f.fileName
            ? {
                ...r,
                signStatus: f.success ? 'signed' : 'failed',
                signMessage: f.success ? undefined : f.verifyReason || f.reason || undefined,
                signedTime: f.signedTime,
                trustedTime: f.trustedTime,
                signatureCount: f.signatureCount,
                validSignatureCount: f.validSignatureCount,
                signatures: f.signatures
              }
            : r
        )
      );
    } else if (ev.targetType === 'summary' && ev.summary) {
      this.percent.set(100);
      this.signResult.set(ev.summary);
      this.showResultDialog.set(true);
    }
  }

  openFolder(path: string): void {
    if (!path) return;
    if (this.desktop.openPath(path)) return;
    navigator.clipboard?.writeText(path).then(
      () => this.messageSuccess('Đã sao chép đường dẫn thư mục kết quả vào clipboard.'),
      () => this.messageWarning('Không mở được thư mục. Đường dẫn: ' + path)
    );
  }
}
