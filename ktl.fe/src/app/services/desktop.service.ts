import { Injectable, isDevMode } from '@angular/core';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class DesktopService {
  private external: any = (window as any).external;
  private pendingPickFolder?: (path: string) => void;
  private pendingPickImage?: (path: string) => void;
  private pendingPickWord?: (path: string) => void;
  private pendingPickExcel?: (path: string) => void;
  private pendingPickPdf?: (paths: string[]) => void;

  constructor() {
    if (this.isDesktop && typeof this.external.receiveMessage === 'function') {
      this.external.receiveMessage((message: string) => this.onMessage(message));
    }
  }

  get isDesktop(): boolean {
    return !!this.external && typeof this.external.sendMessage === 'function';
  }

  get apiBase(): string {
    return isDevMode() ? environment.baseUrl : '';
  }

  pickFolder(): Promise<string> {
    if (!this.isDesktop) return Promise.resolve('');
    return new Promise<string>((resolve) => {
      this.pendingPickFolder = resolve;
      this.external.sendMessage('pickFolder');
    });
  }

  pickImage(): Promise<string> {
    if (!this.isDesktop) return Promise.resolve('');
    return new Promise<string>((resolve) => {
      this.pendingPickImage = resolve;
      this.external.sendMessage('pickImage');
    });
  }

  pickWord(): Promise<string> {
    if (!this.isDesktop) return Promise.resolve('');
    return new Promise<string>((resolve) => {
      this.pendingPickWord = resolve;
      this.external.sendMessage('pickWord');
    });
  }

  pickExcel(): Promise<string> {
    if (!this.isDesktop) return Promise.resolve('');
    return new Promise<string>((resolve) => {
      this.pendingPickExcel = resolve;
      this.external.sendMessage('pickExcel');
    });
  }

  pickPdfFiles(): Promise<string[]> {
    if (!this.isDesktop) return Promise.resolve([]);
    return new Promise<string[]>((resolve) => {
      this.pendingPickPdf = resolve;
      this.external.sendMessage('pickPdf');
    });
  }

  openPath(path: string): boolean {
    if (!this.isDesktop || !path) return false;
    this.external.sendMessage('openPath:' + path);
    return true;
  }

  private onMessage(message: string): void {
    if (typeof message !== 'string') return;
    if (message.startsWith('folder:') && this.pendingPickFolder) {
      const resolve = this.pendingPickFolder;
      this.pendingPickFolder = undefined;
      resolve(message.slice('folder:'.length));
    } else if (message.startsWith('image:') && this.pendingPickImage) {
      const resolve = this.pendingPickImage;
      this.pendingPickImage = undefined;
      resolve(message.slice('image:'.length));
    } else if (message.startsWith('word:') && this.pendingPickWord) {
      const resolve = this.pendingPickWord;
      this.pendingPickWord = undefined;
      resolve(message.slice('word:'.length));
    } else if (message.startsWith('pdf:') && this.pendingPickPdf) {
      const resolve = this.pendingPickPdf;
      this.pendingPickPdf = undefined;
      resolve(message.slice('pdf:'.length).split('|').filter((path) => path));
    } else if (message.startsWith('excel:') && this.pendingPickExcel) {
      const resolve = this.pendingPickExcel;
      this.pendingPickExcel = undefined;
      resolve(message.slice('excel:'.length));
    }
  }
}
