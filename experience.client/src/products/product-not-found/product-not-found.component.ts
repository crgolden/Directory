import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { inject } from '@angular/core';

@Component({
  selector: 'app-product-not-found',
  imports: [RouterLink],
  templateUrl: './product-not-found.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductNotFoundComponent {

  constructor() {
    inject(Title).setTitle('Experience | Product Not Found');
  }
}
