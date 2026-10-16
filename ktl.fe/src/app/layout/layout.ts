import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

import { Topbar } from './topbar';
import { Footer } from './footer';
import { LayoutService } from './service/layout.service';

@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, Topbar, Footer, ToastModule, ConfirmDialogModule],
  templateUrl: './layout.html',
  styleUrl: './layout.css'
})
export class Layout {
  protected readonly layoutService = inject(LayoutService);
}
