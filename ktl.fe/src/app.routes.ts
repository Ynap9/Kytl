import { Routes } from '@angular/router';
import { Layout } from './app/layout/layout';
import { Home } from './app/pages/home/home';

export const routes: Routes = [
  {
    path: '',
    component: Layout,
    children: [
      { path: '', component: Home, data: { title: 'Trang chủ' } },
      {
        path: 'ky-so',
        loadChildren: () => import('./app/pages/ky-so/ky-so.router').then((m) => m.kySoRoutes)
      },
      {
        path: 'tao-van-ban',
        loadChildren: () => import('./app/pages/tao-van-ban/tao-van-ban.router').then((m) => m.taoVanBanRoutes)
      },
      {
        path: 'dau-chu-ky',
        loadChildren: () => import('./app/pages/dau-chu-ky/dau-chu-ky.router').then((m) => m.dauChuKyRoutes)
      },
      {
        path: 'cau-hinh',
        loadChildren: () => import('./app/pages/config/config.router').then((m) => m.configRoutes)
      }
    ]
  }
];
