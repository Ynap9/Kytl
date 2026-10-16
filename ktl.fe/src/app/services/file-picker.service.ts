import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { IBaseResponseWithData } from '../shared/models/request-paging.base.models';
import { IUploadedFile } from '../models/file-upload.models';
import { PATH_PICKER_EXTENSIONS, PathPickerKind } from '../shared/constants/path-picker.constants';
import { Utils } from '../shared/utils';
import { DesktopService } from './desktop.service';
import { FileUploadService } from './file-upload.service';

@Injectable({ providedIn: 'root' })
export class FilePickerService {
  private desktop = inject(DesktopService);
  private fileUploadService = inject(FileUploadService);

  get canBrowse(): boolean {
    return this.desktop.isDesktop;
  }

  extensions(kind: PathPickerKind): string[] {
    return PATH_PICKER_EXTENSIONS[kind];
  }

  accepts(kind: PathPickerKind, fileName: string): boolean {
    const extension = fileName.slice(fileName.lastIndexOf('.')).toLowerCase();
    return this.extensions(kind).includes(extension);
  }

  async browse(kind: PathPickerKind): Promise<string> {
    if (!this.canBrowse) return '';
    const path = await this.openDialog(kind);
    return path ? Utils.normalizeFolderPath(path) : '';
  }

  async browsePdfFiles(): Promise<string[]> {
    if (!this.canBrowse) return [];
    const paths = await this.desktop.pickPdfFiles();
    return paths.map((path) => Utils.normalizeFolderPath(path));
  }

  upload(file: File): Observable<IBaseResponseWithData<IUploadedFile>> {
    return this.fileUploadService.upload(file);
  }

  private openDialog(kind: PathPickerKind): Promise<string> {
    switch (kind) {
      case 'word':
        return this.desktop.pickWord();
      case 'excel':
        return this.desktop.pickExcel();
      case 'pdf':
        return this.desktop.pickPdfFiles().then((paths) => paths[0] ?? '');
      case 'image':
        return this.desktop.pickImage();
      default:
        return this.desktop.pickFolder();
    }
  }
}
