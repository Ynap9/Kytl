import { Routes } from '@angular/router';
import { Layout } from './app/layout/layout';

export const routes: Routes = [
  {
    path: '',
    component: Layout,
    children: [
      { path: '', redirectTo: 'ky-so', pathMatch: 'full' },
      {
        path: 'ky-so',
        loadChildren: () => import('./app/pages/ky-so/ky-so.router').then((m) => m.kySoRoutes)
      },
      {
        path: 'tao-van-ban',
        loadChildren: () => import('./app/pages/tao-van-ban/tao-van-ban.router').then((m) => m.taoVanBanRoutes)
      }
    ]
  }
];
