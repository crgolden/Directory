import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NavMenuComponent } from './nav-menu.component';
import { AuthService } from '../auth/auth.service';
import { By } from '@angular/platform-browser';
import { provideRouter, Routes } from '@angular/router';
import { Component } from '@angular/core';

describe('NavMenuComponent', () => {
  let component: NavMenuComponent;
  let fixture: ComponentFixture<NavMenuComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NavMenuComponent,
        ],
      providers: [{ provide: AuthService, useClass: AuthServiceStub }, provideRouter(testRoutes)],
    })
    .compileComponents();

    fixture = TestBed.createComponent(NavMenuComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  it('should toggle isExpanded when toggle is called', () => {
    expect(component.isExpanded()).toBe(false);
    component.toggle();
    expect(component.isExpanded()).toBe(true);
    component.toggle();
    expect(component.isExpanded()).toBe(false);
  });

  it('should collapse when collapse is called', () => {
    component.isExpanded.set(true);
    component.collapse();
    expect(component.isExpanded()).toBe(false);
  });

  it('should render signout link when authenticated', () => {
    // Signals are automatically evaluated in template with OnPush change detection
    fixture.detectChanges();
    const signoutLink = fixture.debugElement.query(By.css('a.nav-link[href="/logout"]'));
    expect(signoutLink).toBeTruthy();
  });
});
@Component({
  template: ''
})
class DummyComponent {}
// Define dummy routes.  Include routes for ALL paths used by routerLink in your component.
const testRoutes: Routes = [
  { path: '', component: DummyComponent },
  { path: 'products', component: DummyComponent },
  { path: 'chat', component: DummyComponent },
];

class AuthServiceStub {
  isAuthenticated = () => true;
  isAnonymous = () => false;
  logoutUrl = () => '/logout';
}
