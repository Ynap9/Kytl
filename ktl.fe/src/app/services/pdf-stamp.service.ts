import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { IBaseResponseWithData } from '../shared/models/request-paging.base.models';
import {
  IPdfStampFileList,
  IPdfStampPreview,
  IPdfStampPreviewRequest,
  IPdfStampProgress,
  IPdfStampRequest
} from '../models/pdf-stamp.models';
import { API_PREFIX } from '../shared/constants/api.constants';
import { streamSse } from '../shared/sse-stream';
import { DesktopService } from './desktop.service';

@Injectable({ providedIn: 'root' })
export class PdfStampService {
  private http = inject(HttpClient);
  private desktop = inject(DesktopService);
  private get base() {
    return `${this.desktop.apiBase}${API_PREFIX}/core/document/dau-chu-ky`;
  }

  getFiles(sourcePaths: string[]): Observable<IBaseResponseWithData<IPdfStampFileList>> {
    return this.http.post<IBaseResponseWithData<IPdfStampFileList>>(`${this.base}/danh-sach-file`, { sourcePaths });
  }

  preview(request: IPdfStampPreviewRequest): Observable<IBaseResponseWithData<IPdfStampPreview>> {
    return this.http.post<IBaseResponseWithData<IPdfStampPreview>>(`${this.base}/xem-truoc`, request);
  }

  fileUrl(path: string): string {
    return `${this.base}/xem-file?path=${encodeURIComponent(path)}`;
  }

  stamp(request: IPdfStampRequest): Observable<IPdfStampProgress> {
    return streamSse<IPdfStampProgress>(`${this.base}/chen`, request);
  }
}
