import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';

@Component({
  selector: 'app-catalog-not-found',
  imports: [RouterLink],
  templateUrl: './catalog-not-found.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogNotFoundComponent {

  constructor() {
    inject(Title).setTitle('Experience | Product Not Found');
  }
}
