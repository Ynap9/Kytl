import { Routes } from '@angular/router';
import { KySo } from './ky-so';

export const kySoRoutes: Routes = [
  { path: '', component: KySo, data: { title: 'Ký số tài liệu', reuse: true } }
];
