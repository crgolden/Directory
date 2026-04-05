import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HomeComponent } from './home.component';
import { AuthService } from '../auth/auth.service';
import { By } from '@angular/platform-browser';
import { provideRouter, Routes } from '@angular/router';
import { Component } from '@angular/core';
import { signal } from '@angular/core';

@Component({ template: '' })
class DummyComponent {}

const testRoutes: Routes = [
  { path: '', component: DummyComponent },
  { path: 'products', component: DummyComponent },
];

describe('HomeComponent', () => {
  let fixture: ComponentFixture<HomeComponent>;

  const setup = async (isAuthenticated: boolean) => {
    await TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [
        { provide: AuthService, useValue: { isAuthenticated: signal(isAuthenticated) } },
        provideRouter(testRoutes),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(HomeComponent);
    fixture.detectChanges();
  };

  it('renders the hero headline', async () => {
    await setup(false);
    const h1 = fixture.debugElement.query(By.css('h1'));
    expect(h1.nativeElement.textContent).toContain('Your Complete Product Inventory');
  });

  it('renders six benefit cards', async () => {
    await setup(false);
    const cards = fixture.debugElement.queryAll(By.css('.card'));
    expect(cards.length).toBe(6);
  });

  it('shows login CTA when anonymous', async () => {
    await setup(false);
    const loginLink = fixture.debugElement.query(By.css('a[href="/bff/login"]'));
    expect(loginLink).toBeTruthy();
    expect(loginLink.nativeElement.textContent).toContain('Log In');
  });

  it('shows products link when authenticated', async () => {
    await setup(true);
    const productsLink = fixture.debugElement.query(By.css('a[href="/products"]'));
    expect(productsLink).toBeTruthy();
    expect(productsLink.nativeElement.textContent).toContain('View My Products');
  });
});
