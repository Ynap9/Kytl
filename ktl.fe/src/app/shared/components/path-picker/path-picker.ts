import { Component, computed, inject, input, model, signal } from '@angular/core';

import { SharedImports } from '../../import.shared';
import { BaseComponent } from '../base/base-component';
import { Utils } from '../../utils';
import { FilePickerService } from '../../../services/file-picker.service';
import { PATH_PICKER_DROPPED_LABEL, PathPickerKind } from '../../constants/path-picker.constants';

@Component({
  selector: 'app-path-picker',
  imports: [...SharedImports],
  templateUrl: './path-picker.html'
})
export class PathPicker extends BaseComponent {
  protected filePicker = inject(FilePickerService);

  value = model<string>('');
  kind = input.required<PathPickerKind>();
  label = input<string>('file');
  hint = input<string>('');
  icon = input<string>('');
  iconClass = input<string>('bg-slate-100 text-slate-500');
  invalid = input<boolean>(false);
  disabled = input<boolean>(false);
  compact = input<boolean>(false);

  dragOver = signal<boolean>(false);
  uploading = signal<boolean>(false);
  private droppedPath = signal<string | null>(null);

  extensions = computed(() => this.filePicker.extensions(this.kind()));
  droppable = computed(() => this.extensions().length > 0 && !this.disabled());
  showDropzone = computed(() => this.droppable() && !this.compact() && !this.value());
  pickLabel = computed(() => (this.kind() === 'folder' ? 'Chọn thư mục' : 'Chọn file'));
  fileName = computed(() => Utils.normalizeFolderPath(this.value()).split('/').pop() ?? '');
  pathText = computed(() =>
    this.value() && this.value() === this.droppedPath() ? PATH_PICKER_DROPPED_LABEL : this.value()
  );

  async pick(): Promise<void> {
    if (this.disabled() || this.uploading()) return;
    if (!this.filePicker.canBrowse) {
      this.messageWarning('Hộp thoại chọn chỉ có trên ứng dụng desktop. Hãy kéo thả hoặc dán đường dẫn để thử trên web.');
      return;
    }
    const path = await this.filePicker.browse(this.kind());
    if (path) this.setValue(path);
  }

  clear(): void {
    if (!this.disabled()) this.setValue('');
  }

  setValue(path: string): void {
    this.droppedPath.set(null);
    this.value.set(Utils.normalizeFolderPath(path ?? ''));
  }

  onDragOver(event: DragEvent): void {
    if (!this.droppable()) return;
    event.preventDefault();
    this.dragOver.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(false);
  }

  onDrop(event: DragEvent): void {
    if (!this.droppable()) return;
    event.preventDefault();
    this.dragOver.set(false);

    const file = event.dataTransfer?.files?.[0];
    if (!file || this.uploading()) return;

    if (!this.filePicker.accepts(this.kind(), file.name)) {
      this.messageWarning(`Chỉ nhận file ${this.extensions().join(', ')}.`);
      return;
    }

    this.uploading.set(true);
    this.filePicker.upload(file).subscribe({
      next: (res) => {
        this.uploading.set(false);
        if (!this.isResponseSucceed(res)) return;
        this.value.set(res.data.filePath);
        this.droppedPath.set(res.data.filePath);
      },
      error: () => {
        this.uploading.set(false);
        this.messageError('Không đưa được file vào ứng dụng. Vui lòng thử lại hoặc bấm để chọn file.');
      }
    });
  }
}
