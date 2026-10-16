import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { IBaseResponseWithData } from '../shared/models/request-paging.base.models';
import {
  ISignCert,
  ISignFileList,
  ISignPositionCheck,
  ISignPositionCheckRequest,
  ISignPreview,
  ISignProgress,
  ISignRequest,
  ITokenVerify
} from '../models/sign.models';
import { API_PREFIX } from '../shared/constants/api.constants';
import { DesktopService } from './desktop.service';

@Injectable({ providedIn: 'root' })
export class SignService {
  private http = inject(HttpClient);
  private desktop = inject(DesktopService);
  private get base() {
    return `${this.desktop.apiBase}${API_PREFIX}`;
  }

  getCertificates(onlySignable = false): Observable<IBaseResponseWithData<ISignCert[]>> {
    return this.http.get<IBaseResponseWithData<ISignCert[]>>(
      `${this.base}/core/document/sign/chung-thu-so`,
      { params: new HttpParams().set('onlySignable', onlySignable) }
    );
  }

  verifyToken(thumbprint: string): Observable<IBaseResponseWithData<ITokenVerify>> {
    return this.http.post<IBaseResponseWithData<ITokenVerify>>(
      `${this.base}/core/document/sign/kiem-tra-token`,
      { thumbprint }
    );
  }

  getPreview(sourcePath: string, index: number): Observable<IBaseResponseWithData<ISignPreview>> {
    return this.http.get<IBaseResponseWithData<ISignPreview>>(
      `${this.base}/core/document/sign/xem-truoc`,
      { params: new HttpParams().set('sourcePath', sourcePath).set('index', index) }
    );
  }

  checkPositions(request: ISignPositionCheckRequest): Observable<IBaseResponseWithData<ISignPositionCheck>> {
    return this.http.post<IBaseResponseWithData<ISignPositionCheck>>(
      `${this.base}/core/document/sign/kiem-tra-vi-tri`,
      request
    );
  }

  pdfFileUrl(fullPath: string): string {
    return `${this.base}/core/document/sign/xem-file?path=${encodeURIComponent(fullPath)}`;
  }

  getFiles(sourcePaths: string[]): Observable<IBaseResponseWithData<ISignFileList>> {
    return this.http.post<IBaseResponseWithData<ISignFileList>>(
      `${this.base}/core/document/sign/danh-sach-file`,
      { sourcePaths }
    );
  }

  signFolder(req: ISignRequest): Observable<ISignProgress> {
    return this.streamSse<ISignProgress>(
      `${this.base}/core/document/sign/ky-thu-muc`,
      req
    );
  }

  private streamSse<T>(url: string, body: unknown): Observable<T> {
    return new Observable<T>((subscriber) => {
      const controller = new AbortController();

      (async () => {
        try {
          const res = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
            signal: controller.signal
          });

          if (!res.ok || !res.body) {
            subscriber.error(new Error(`HTTP ${res.status}`));
            return;
          }

          if (res.headers.get('Content-Type')?.includes('application/json')) {
            subscriber.error(await res.json());
            return;
          }

          const reader = res.body.getReader();
          const decoder = new TextDecoder();
          let buffer = '';

          while (true) {
            const { value, done } = await reader.read();
            if (done) break;
            buffer += decoder.decode(value, { stream: true });

            let idx: number;
            while ((idx = buffer.indexOf('\n\n')) >= 0) {
              const chunk = buffer.slice(0, idx).trim();
              buffer = buffer.slice(idx + 2);
              if (chunk.startsWith('data:')) {
                const json = chunk.slice(5).trim();
                if (json) {
                  subscriber.next(JSON.parse(json) as T);
                }
              }
            }
          }

          subscriber.complete();
        } catch (err) {
          if (!controller.signal.aborted) {
            subscriber.error(err);
          }
        }
      })();

      return () => controller.abort();
    });
  }
}
