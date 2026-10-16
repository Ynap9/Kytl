import { Component } from '@angular/core';

@Component({
  selector: 'app-footer',
  template: `
    <div class="layout-statusbar">
      <span class="flex items-center gap-2">
        <i class="pi pi-circle-fill text-green-500" style="font-size: 0.5rem"></i>
        Sẵn sàng
      </span>
      <span>KTL · Trường Đại học Xây dựng Hà Nội</span>
    </div>
  `
})
export class Footer {}
