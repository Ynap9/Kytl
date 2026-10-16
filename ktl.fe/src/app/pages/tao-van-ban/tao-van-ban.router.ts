import { Routes } from '@angular/router';
import { TaoVanBan } from './tao-van-ban';
import { MERGE_PAGE_TITLE } from '../../shared/constants/document-merge.constants';

export const taoVanBanRoutes: Routes = [
  { path: '', component: TaoVanBan, data: { title: MERGE_PAGE_TITLE, reuse: true } }
];
