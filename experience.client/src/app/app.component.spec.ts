import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppComponent } from './app.component';
import { RouterOutlet } from '@angular/router';
import { NavMenuComponent } from '../nav-menu/nav-menu.component';
import { Component, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { provideRouter, Routes } from '@angular/router';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AuthService } from '../auth/auth.service';

@Component({ template: '' })
class DummyComponent {}

const testRoutes: Routes = [
  { path: '', component: DummyComponent },
  { path: 'user-session', component: DummyComponent },
];

describe('AppComponent', () => {
  let component: AppComponent;
  let fixture: ComponentFixture<AppComponent>;

  const setup = async (isAuthenticated: boolean) => {
    await TestBed.configureTestingModule({
      imports: [AppComponent, RouterOutlet, NavMenuComponent],
      providers: [
        provideRouter(testRoutes),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            isAuthenticated: signal(isAuthenticated),
            isAnonymous: signal(!isAuthenticated),
            logoutUrl: signal(null),
            loginUrl: '/bff/login',
            refresh: vi.fn(),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  };

  it('should create the app', async () => {
    await setup(false);
    expect(component).toBeTruthy();
  });

  it('should render nav-menu', async () => {
    await setup(false);
    const navMenu = fixture.debugElement.query(By.directive(NavMenuComponent));
    expect(navMenu).toBeTruthy();
  });

  it('should render router-outlet', async () => {
    await setup(false);
    const routerOutlet = fixture.debugElement.query(By.directive(RouterOutlet));
    expect(routerOutlet).toBeTruthy();
  });

  it('shows silent login iframe when unauthenticated', async () => {
    await setup(false);
    expect(component.iframeVisible()).toBe(true);
    expect(component.iframeUrl()).not.toBeNull();
    const iframe = fixture.debugElement.query(By.css('#bff-silent-login'));
    expect(iframe).toBeTruthy();
  });

  it('does not show silent login iframe when authenticated', async () => {
    await setup(true);
    expect(component.iframeVisible()).toBe(false);
    const iframe = fixture.debugElement.query(By.css('#bff-silent-login'));
    expect(iframe).toBeNull();
  });

  it('onMessage hides iframe and calls refresh when isLoggedIn is true', async () => {
    await setup(false);
    const authService = TestBed.inject(AuthService);
    expect(component.iframeVisible()).toBe(true);

    component.onMessage(new MessageEvent('message', {
      data: { source: 'bff-silent-login', isLoggedIn: true }
    }));
    fixture.detectChanges();

    expect(component.iframeVisible()).toBe(false);
    expect(authService.refresh).toHaveBeenCalledOnce();
  });

  it('onMessage hides iframe but does not call refresh when isLoggedIn is false', async () => {
    await setup(false);
    const authService = TestBed.inject(AuthService);

    component.onMessage(new MessageEvent('message', {
      data: { source: 'bff-silent-login', isLoggedIn: false }
    }));
    fixture.detectChanges();

    expect(component.iframeVisible()).toBe(false);
    expect(authService.refresh).not.toHaveBeenCalled();
  });

  it('onMessage ignores messages from unknown sources', async () => {
    await setup(false);
    const authService = TestBed.inject(AuthService);
    const visibleBefore = component.iframeVisible();

    component.onMessage(new MessageEvent('message', {
      data: { source: 'something-else', isLoggedIn: true }
    }));

    expect(component.iframeVisible()).toBe(visibleBefore);
    expect(authService.refresh).not.toHaveBeenCalled();
  });
});
