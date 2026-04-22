import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { debounceTime, distinctUntilChanged, Subject, switchMap } from 'rxjs';
import { CatalogService } from '../catalog.service';
import { Product } from '../../products/product.model';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-catalog-list',
  imports: [RouterLink, FormsModule],
  templateUrl: './catalog-list.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogListComponent implements OnInit {

  private readonly titleService = inject(Title);
  private readonly catalogService = inject(CatalogService);

  readonly items = signal<Product[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly searchTerm = signal('');
  readonly orderBy = signal('Name');
  readonly orderDir = signal<'asc' | 'desc'>('asc');
  readonly page = signal(1);

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));
  readonly showingFrom = computed(() =>
    this.items().length === 0 ? 0 : (this.page() - 1) * PAGE_SIZE + 1
  );
  readonly showingTo = computed(() => (this.page() - 1) * PAGE_SIZE + this.items().length);

  private readonly search$ = new Subject<string>();

  ngOnInit(): void {
    this.titleService.setTitle('Experience | Catalog');

    this.search$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(term => {
        this.loading.set(true);
        this.page.set(1);
        return this.catalogService.getAll({
          search: term,
          orderBy: this.orderBy(),
          orderDir: this.orderDir(),
          page: 1,
          pageSize: PAGE_SIZE,
        });
      })
    ).subscribe(result => {
      this.items.set(result.items);
      this.total.set(result.total);
      this.loading.set(false);
    });

    this.loadPage();
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.search$.next(term);
  }

  sortBy(column: string): void {
    if (this.orderBy() === column) {
      this.orderDir.update(d => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      this.orderBy.set(column);
      this.orderDir.set('asc');
    }
    this.page.set(1);
    this.loadPage();
  }

  prevPage(): void {
    if (this.page() > 1) {
      this.page.update(p => p - 1);
      this.loadPage();
    }
  }

  nextPage(): void {
    if (this.page() < this.totalPages()) {
      this.page.update(p => p + 1);
      this.loadPage();
    }
  }

  private loadPage(): void {
    this.loading.set(true);
    this.catalogService.getAll({
      search: this.searchTerm(),
      orderBy: this.orderBy(),
      orderDir: this.orderDir(),
      page: this.page(),
      pageSize: PAGE_SIZE,
    }).subscribe(result => {
      this.items.set(result.items);
      this.total.set(result.total);
      this.loading.set(false);
    });
  }
}
