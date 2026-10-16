import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { IBaseResponseWithData } from '../shared/models/request-paging.base.models';
import {
  IMergeMapping,
  IMergePreview,
  IMergePreviewRequest,
  IMergeProgress,
  IMergeRequest,
  IMergeValidation
} from '../models/document-merge.models';
import { API_PREFIX } from '../shared/constants/api.constants';
import { streamSse } from '../shared/sse-stream';
import { DesktopService } from './desktop.service';

@Injectable({ providedIn: 'root' })
export class DocumentMergeService {
  private http = inject(HttpClient);
  private desktop = inject(DesktopService);
  private get base() {
    return `${this.desktop.apiBase}${API_PREFIX}/core/document/tao-van-ban`;
  }

  autoMap(templatePath: string, dataPath: string): Observable<IBaseResponseWithData<IMergeMapping>> {
    return this.http.get<IBaseResponseWithData<IMergeMapping>>(`${this.base}/map-tu-dong`, {
      params: new HttpParams().set('templatePath', templatePath).set('dataPath', dataPath)
    });
  }

  validate(request: IMergeRequest): Observable<IBaseResponseWithData<IMergeValidation>> {
    return this.http.post<IBaseResponseWithData<IMergeValidation>>(`${this.base}/kiem-tra`, request);
  }

  preview(request: IMergePreviewRequest): Observable<IBaseResponseWithData<IMergePreview>> {
    return this.http.post<IBaseResponseWithData<IMergePreview>>(`${this.base}/xem-truoc`, request);
  }

  previewFileUrl(filePath: string): string {
    return `${this.base}/xem-file?path=${encodeURIComponent(filePath)}`;
  }

  merge(request: IMergeRequest): Observable<IMergeProgress> {
    return streamSse<IMergeProgress>(`${this.base}/sinh-file`, request);
  }
}
