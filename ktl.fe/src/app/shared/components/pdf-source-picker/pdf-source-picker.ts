import { Component, inject, input, model, signal } from '@angular/core';
import { forkJoin } from 'rxjs';

import { SharedImports } from '../../import.shared';
import { BaseComponent } from '../base/base-component';
import { Utils } from '../../utils';
import { FilePickerService } from '../../../services/file-picker.service';

@Component({
  selector: 'app-pdf-source-picker',
  imports: [...SharedImports],
  templateUrl: './pdf-source-picker.html',
  styleUrl: './pdf-source-picker.scss'
})
export class PdfSourcePicker extends BaseComponent {
  protected filePicker = inject(FilePickerService);

  title = input<string>('File PDF');
  droppable = input<boolean>(true);
  disabled = input<boolean>(false);
  sources = model<string[]>([]);
  droppedSources = model<string[]>([]);

  sourceInput = signal<string>('');
  dragOver = signal<boolean>(false);
  uploading = signal<boolean>(false);

  sourceName(path: string): string {
    return path.split('/').pop() || path;
  }

  isFolderSource(path: string): boolean {
    return !path.toLowerCase().endsWith('.pdf');
  }

  isDropped(path: string): boolean {
    return this.droppedSources().includes(path);
  }

  async addFiles(): Promise<void> {
    if (!this.ensureDesktop()) return;
    this.addSources(await this.filePicker.browsePdfFiles());
  }

  async addFolder(): Promise<void> {
    if (!this.ensureDesktop()) return;
    const path = await this.filePicker.browse('folder');
    if (path) this.addSources([path]);
  }

  addSourceInput(): void {
    this.addSources(this.sourceInput().split(/\r?\n/));
    this.sourceInput.set('');
  }

  onPaste(event: ClipboardEvent): void {
    const text = event.clipboardData?.getData('text') ?? '';
    if (!text.trim()) return;
    event.preventDefault();
    this.addSources(text.split(/\r?\n/));
    this.sourceInput.set('');
  }

  removeSource(path: string): void {
    this.sources.update((list) => list.filter((source) => source !== path));
  }

  clearSources(): void {
    this.sources.set([]);
  }

  onDragOver(event: DragEvent): void {
    if (!this.droppable() || this.disabled() || !event.dataTransfer?.types.includes('Files')) return;
    event.preventDefault();
    this.dragOver.set(true);
  }

  onDragLeave(): void {
    this.dragOver.set(false);
  }

  onDrop(event: DragEvent): void {
    const dropped = Array.from(event.dataTransfer?.files ?? []);
    this.dragOver.set(false);
    if (!this.droppable() || this.disabled() || dropped.length === 0) return;
    event.preventDefault();

    const pdfs = dropped.filter((file) => this.filePicker.accepts('pdf', file.name));
    if (pdfs.length < dropped.length) {
      this.messageWarning('Chỉ nhận file PDF, các file khác đã được bỏ qua.');
    }
    if (pdfs.length === 0) return;

    this.uploading.set(true);
    forkJoin(pdfs.map((file) => this.filePicker.upload(file))).subscribe({
      next: (responses) => {
        this.uploading.set(false);
        const paths = responses
          .filter((res) => this.isResponseSucceed(res))
          .map((res) => Utils.normalizeFolderPath(res.data.filePath));
        this.droppedSources.update((list) => [...list, ...paths]);
        this.addSources(paths);
      },
      error: () => {
        this.uploading.set(false);
        this.messageError('Không đưa được file vào ứng dụng. Vui lòng thử lại hoặc bấm Chọn file.');
      }
    });
  }

  private addSources(paths: string[]): void {
    const normalized = paths.map((path) => Utils.normalizeFolderPath(path)).filter((path) => path);
    if (normalized.length === 0) return;
    this.sources.update((list) => [...list, ...normalized.filter((path) => !list.includes(path))]);
  }

  private ensureDesktop(): boolean {
    if (this.filePicker.canBrowse) return true;
    this.messageWarning('Hộp thoại chọn chỉ có trên ứng dụng desktop. Hãy dán đường dẫn để thử trên web.');
    return false;
  }
}
