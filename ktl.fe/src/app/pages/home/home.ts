import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

interface HomeFeature {
  title: string;
  description: string;
  icon: string;
  link: string;
  iconClass: string;
  points: string[];
}

interface HomeFlowStep {
  title: string;
  description: string;
  icon: string;
  link: string;
}

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  templateUrl: './home.html'
})
export class Home {
  protected readonly features: HomeFeature[] = [
    {
      title: 'Ký số',
      description: 'Ký số hàng loạt file PDF bằng chứng thư số USB token, kèm dấu thời gian.',
      icon: 'pi-verified',
      link: '/ky-so',
      iconClass: 'bg-indigo-50 text-indigo-600',
      points: ['Ký cả thư mục', 'Kiểm tra chữ ký sau ký', 'Theo template cấu hình']
    },
    {
      title: 'Tạo văn bản',
      description: 'Ghép template Word với dữ liệu Excel để sinh hàng loạt văn bản PDF.',
      icon: 'pi-clone',
      link: '/tao-van-ban',
      iconClass: 'bg-sky-50 text-sky-600',
      points: ['Tự map trường dữ liệu', 'Xem trước từng dòng', 'Chèn dấu khi sinh file']
    },
    {
      title: 'Dấu & chữ ký',
      description: 'Chèn ảnh dấu đỏ, chữ ký tươi vào file PDF theo trang hoặc lặp nhiều trang.',
      icon: 'pi-images',
      link: '/dau-chu-ky',
      iconClass: 'bg-rose-50 text-rose-600',
      points: ['Lặp chẵn, lẻ, tùy chọn', 'Vị trí riêng từng trang', 'Giữ nguyên file gốc']
    },
    {
      title: 'Cấu hình',
      description: 'Quản lý chứng thư số và template vị trí chữ ký, dấu đỏ dùng khi ký.',
      icon: 'pi-cog',
      link: '/cau-hinh',
      iconClass: 'bg-amber-50 text-amber-600',
      points: ['Template chữ ký', 'Chứng thư số trên máy', 'Font, cỡ, màu chữ ký']
    }
  ];

  protected readonly flowSteps: HomeFlowStep[] = [
    { title: 'Tạo văn bản', description: 'Sinh PDF từ Word + Excel', icon: 'pi-clone', link: '/tao-van-ban' },
    { title: 'Dấu & chữ ký', description: 'Chèn dấu đỏ, chữ ký tươi', icon: 'pi-images', link: '/dau-chu-ky' },
    { title: 'Ký số', description: 'Ký và gắn dấu thời gian', icon: 'pi-verified', link: '/ky-so' }
  ];
}
