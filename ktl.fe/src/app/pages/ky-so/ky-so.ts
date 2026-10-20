import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { MenuItem } from 'primeng/api';
import { TagModule } from 'primeng/tag';

import { SharedImports } from '../../shared/import.shared';
import { BaseComponent } from '../../shared/components/base/base-component';
import { Breadcrumb } from '../../shared/components/breadcrumb/breadcrumb';
import { PdfSourcePicker } from '../../shared/components/pdf-source-picker/pdf-source-picker';
import { SignService } from '../../services/sign.service';
import { SignHandoffService } from '../../services/sign-handoff.service';
import { SignCertStateService } from '../../services/sign-cert-state.service';
import { SignConfigStateService } from '../../services/sign-config-state.service';
import { ISignCert, ISignConfig } from '../../models/sign.models';
import { SignStep } from './sign-step/sign-step';
import { SignConfigDialog } from './sign-config-dialog/sign-config-dialog';

@Component({
  selector: 'app-ky-so',
  imports: [...SharedImports, TagModule, Breadcrumb, PdfSourcePicker, SignStep, SignConfigDialog],
  templateUrl: './ky-so.html',
  styleUrl: './ky-so.scss'
})
export class KySo extends BaseComponent implements OnInit {
  private signService = inject(SignService);
  private certState = inject(SignCertStateService);
  private signHandoff = inject(SignHandoffService);
  private configState = inject(SignConfigStateService);

  home: MenuItem = { icon: 'pi pi-home', routerLink: '/' };
  breadcrumbItems: MenuItem[] = [{ label: 'Ký số' }];

  certs = signal<ISignCert[]>([]);
  certLoading = signal<boolean>(false);
  thumbprint = signal<string | null>(null);
  verifying = signal<boolean>(false);

  sources = signal<string[]>([]);
  showConfig = signal<boolean>(false);

  config = this.configState.config;

  configSummary = computed<string[]>(() => {
    const config = this.config();
    const paths = new Set(this.sources());
    const ownCount = config.fileOverrides.filter((item) => paths.has(item.path)).length;
    return [
      ...(config.reason ? [`Lý do: ${config.reason}`] : []),
      ...(config.location ? [`Nơi ký: ${config.location}`] : []),
      config.showSignature ? `${config.fontFamily}, ${config.fontSize} pt` : 'Ẩn chữ ký số',
      ...(config.stamps.length > 0 ? [`${config.stamps.length} dấu, chữ ký tươi`] : []),
      ...(config.embedSignatureInImage ? ['Nhồi vào ảnh'] : []),
      ...(ownCount > 0 ? [`${ownCount} file riêng`] : [])
    ];
  });

  certName = computed<string>(
    () => this.certs().find((cert) => cert.thumbprint === this.thumbprint())?.commonName || ''
  );

  isVerified = computed<boolean>(
    () => !!this.thumbprint() && this.certState.verifiedCert()?.thumbprint === this.thumbprint()
  );

  override ngOnInit(): void {
    this.takeHandoff();
    this.loadCertificates();
  }

  override onRouteActivated(): void {
    this.takeHandoff();
  }

  loadCertificates(): void {
    this.certLoading.set(true);
    this.signService.getCertificates(true).subscribe({
      next: (res) => {
        this.certs.set(this.isResponseSucceed(res) ? (res.data ?? []) : []);
        this.certLoading.set(false);
      },
      error: () => {
        this.messageError('Không thể đọc chứng thư số trên máy. Vui lòng thử lại sau.');
        this.certs.set([]);
        this.certLoading.set(false);
      }
    });
  }

  onCertChange(thumbprint: string | null): void {
    this.thumbprint.set(thumbprint);
  }

  onConfigSave(config: ISignConfig): void {
    this.configState.save(config);
    this.messageSuccess('Đã lưu cấu hình chữ ký.');
  }

  verify(): void {
    const thumbprint = this.thumbprint();
    if (!thumbprint || this.verifying()) return;

    this.verifying.set(true);
    this.signService.verifyToken(thumbprint).subscribe({
      next: (res) => {
        this.verifying.set(false);
        if (!this.isResponseSucceed(res)) return;
        if (res.data?.valid) {
          this.certState.verifiedCert.set(res.data);
          this.messageSuccess('Chứng thư số đã sẵn sàng để ký.');
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

  private takeHandoff(): void {
    const paths = this.signHandoff.take();
    if (paths.length > 0) this.sources.set(paths);
  }
}
