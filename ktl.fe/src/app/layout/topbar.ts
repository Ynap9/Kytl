import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { Menu } from './menu';

@Component({
  selector: 'app-topbar',
  imports: [RouterLink, Menu],
  template: `
    <div class="layout-topbar">
      <a class="layout-topbar-logo" routerLink="/" draggable="false">
        <i class="pi pi-file-edit"></i>
        <span>KTL</span>
      </a>

      <span class="layout-topbar-divider"></span>

      <app-menu />
    </div>
  `
})
export class Topbar {}
