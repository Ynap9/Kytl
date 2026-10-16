import { Component, computed, inject, input, model, output, signal } from '@angular/core';

import { SharedImports } from '../../../shared/import.shared';
import { BaseComponent } from '../../../shared/components/base/base-component';
import { DocumentMergeService } from '../../../services/document-merge.service';
import { IMergeFieldMapping, IMergeMapping, IMergeValidation } from '../../../models/document-merge.models';

interface IFieldStatus {
  label: string;
  pillClass: string;
  barClass: string;
}

const FIELD_STATUS: Record<'auto' | 'manual' | 'unmapped', IFieldStatus> = {
  auto: { label: '✓ Tự động', pillClass: 'bg-[#ddf1e5] text-[#157a42]', barClass: 'bg-[#1f9d55]' },
  manual: { label: 'Đã chọn', pillClass: 'bg-[#e3ecfa] text-[var(--layout-accent)]', barClass: 'bg-[var(--layout-accent)]' },
  unmapped: { label: 'Chưa ghép', pillClass: 'bg-[#fbeacf] text-[#b26a00]', barClass: 'bg-[#d98300]' }
};

@Component({
  selector: 'app-merge-mapping-step',
  imports: [...SharedImports],
  templateUrl: './merge-mapping-step.html',
  styleUrl: './merge-mapping-step.scss',
  host: { class: 'flex min-h-0 flex-1 flex-col' }
})
export class MergeMappingStep extends BaseComponent {
  private documentMergeService = inject(DocumentMergeService);

  templatePath = input<string>('');
  dataPath = input<string>('');
  templateName = input<string>('');
  validation = input<IMergeValidation | null>(null);
  validating = input<boolean>(false);
  mapping = model<IMergeMapping | null>(null);
  fields = model<IMergeFieldMapping[]>([]);
  fileNameColumnNumber = model<number | null>(null);
  validate = output<void>();

  remapping = signal<boolean>(false);

  headers = computed(() => this.mapping()?.headers ?? []);
  mappedCount = computed(() => this.fields().filter((field) => field.columnNumber !== null).length);
  allMapped = computed(() => this.fields().length > 0 && this.mappedCount() === this.fields().length);

  fileNameExample = computed(() => {
    const header = this.headers().find((h) => h.columnNumber === this.fileNameColumnNumber());
    return `${this.templateName()}_${header ? `<${header.name}>` : '<số dòng Excel>'}.pdf`;
  });

  fieldStatus(field: IMergeFieldMapping): IFieldStatus {
    if (field.columnNumber === null) return FIELD_STATUS.unmapped;
    return field.autoMapped ? FIELD_STATUS.auto : FIELD_STATUS.manual;
  }

  onColumnChange(index: number, columnNumber: number | null): void {
    const header = this.headers().find((h) => h.columnNumber === columnNumber);
    this.fields.update((list) =>
      list.map((field, i) =>
        i === index
          ? { ...field, columnNumber: columnNumber ?? null, headerName: header?.name ?? null, autoMapped: false }
          : field
      )
    );
  }

  clearMapping(): void {
    this.confirmAction({ header: 'Xoá ghép', message: 'Bỏ ghép cột cho tất cả các trường?' }, () =>
      this.fields.update((list) =>
        list.map((field) => ({ ...field, columnNumber: null, headerName: null, autoMapped: false }))
      )
    );
  }

  remap(): void {
    if (this.remapping()) return;
    this.remapping.set(true);
    this.documentMergeService.autoMap(this.templatePath(), this.dataPath()).subscribe({
      next: (res) => {
        this.remapping.set(false);
        if (!this.isResponseSucceed(res, true, 'Đã ghép tự động theo tên cột Excel.')) return;
        this.mapping.set(res.data);
        this.fields.set(res.data.fields);
      },
      error: () => {
        this.remapping.set(false);
        this.messageError('Không kết nối được tới dịch vụ tạo văn bản. Vui lòng thử lại sau.');
      }
    });
  }
}
