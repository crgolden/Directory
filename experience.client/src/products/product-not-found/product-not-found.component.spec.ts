import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter, Routes } from '@angular/router';
import { Component } from '@angular/core';
import { ProductNotFoundComponent } from './product-not-found.component';

@Component({ template: '' })
class DummyComponent {}

const testRoutes: Routes = [{ path: 'products', component: DummyComponent }];

describe('ProductNotFoundComponent', () => {
  let fixture: ComponentFixture<ProductNotFoundComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductNotFoundComponent],
      providers: [provideRouter(testRoutes)],
    }).compileComponents();

    fixture = TestBed.createComponent(ProductNotFoundComponent);
    fixture.detectChanges();
  });

  it('renders the not-found heading', () => {
    const h2 = fixture.debugElement.query(By.css('h2'));
    expect(h2.nativeElement.textContent).toContain('Product Not Found');
  });

  it('renders a link back to /products', () => {
    const link: HTMLAnchorElement = fixture.debugElement.query(By.css('a')).nativeElement;
    expect(link.textContent).toContain('Back to My Products');
  });
});
