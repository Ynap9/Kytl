import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { IBaseResponseWithData } from '../shared/models/request-paging.base.models';
import { IUploadedFile } from '../models/file-upload.models';
import { API_PREFIX } from '../shared/constants/api.constants';
import { DesktopService } from './desktop.service';

@Injectable({ providedIn: 'root' })
export class FileUploadService {
  private http = inject(HttpClient);
  private desktop = inject(DesktopService);
  private get base() {
    return `${this.desktop.apiBase}${API_PREFIX}/core/document/tai-len`;
  }

  upload(file: File): Observable<IBaseResponseWithData<IUploadedFile>> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<IBaseResponseWithData<IUploadedFile>>(this.base, body);
  }
}
