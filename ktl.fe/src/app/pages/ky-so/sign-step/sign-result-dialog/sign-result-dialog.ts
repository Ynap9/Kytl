import { Component, computed, input, model, output } from '@angular/core';

import { SharedImports } from '../../../../shared/import.shared';
import { BaseComponent } from '../../../../shared/components/base/base-component';
import { ISignResult } from '../../../../models/sign.models';

type SignResultTone = 'success' | 'error';
interface ISignResultStatus {
  tone: SignResultTone;
  icon: string;
  title: string;
  subtitle: string;
}

@Component({
  selector: 'app-sign-result-dialog',
  imports: [...SharedImports],
  templateUrl: './sign-result-dialog.html',
  styleUrl: './sign-result-dialog.scss'
})
export class SignResultDialog extends BaseComponent {
  visible = model<boolean>(false);
  result = input<ISignResult | null>(null);
  openFolder = output<string>();

  status = computed<ISignResultStatus>(() => {
    const r = this.result();
    const failed = r?.failedCount ?? 0;

    if (failed > 0) {
      return {
        tone: 'error',
        icon: 'pi-exclamation-triangle',
        title: 'Ký số hoàn tất — có lỗi',
        subtitle: `${failed} file gặp lỗi khi ký, cần kiểm tra và ký lại.`
      };
    }
    return {
      tone: 'success',
      icon: 'pi-check',
      title: 'Ký số thành công',
      subtitle: 'Tất cả file đã được ký số và tự nghiệm thu thành công.'
    };
  });

  onOpenFolder(): void {
    const folder = this.result()?.outputFolder;
    if (folder) this.openFolder.emit(folder);
  }

  onClose(): void {
    this.visible.set(false);
  }
}
