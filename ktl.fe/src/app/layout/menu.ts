import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

interface NavItem {
  label: string;
  icon: string;
  link: string;
}

@Component({
  selector: 'app-menu',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="layout-topbar-nav">
      @for (item of navItems; track item.link) {
        <a
          [routerLink]="item.link"
          routerLinkActive="is-active"
          [routerLinkActiveOptions]="{ exact: item.link === '/' }"
          class="layout-topbar-nav-item"
          draggable="false"
        >
          <i class="pi" [class]="item.icon"></i>
          <span>{{ item.label }}</span>
        </a>
      }
    </nav>
  `
})
export class Menu {
  protected readonly navItems: NavItem[] = [
    { label: 'Ký số', icon: 'pi-verified', link: '/ky-so' },
    { label: 'Tạo văn bản', icon: 'pi-clone', link: '/tao-van-ban' }
  ];
}
