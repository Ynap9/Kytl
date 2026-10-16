import { Routes } from '@angular/router';
import { DauChuKy } from './dau-chu-ky';
import { PDF_STAMP_PAGE_TITLE } from '../../shared/constants/pdf-stamp.constants';

export const dauChuKyRoutes: Routes = [
  { path: '', component: DauChuKy, data: { title: PDF_STAMP_PAGE_TITLE, reuse: true } }
];
