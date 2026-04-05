import { Component, inject, signal, ChangeDetectionStrategy, WritableSignal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'nav-menu',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './nav-menu.component.html',
  styleUrl: './nav-menu.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NavMenuComponent {

  private readonly authService: AuthService = inject(AuthService);

  public readonly isAuthenticated = this.authService.isAuthenticated;
  public readonly isAnonymous = this.authService.isAnonymous;
  public readonly logoutUrl = this.authService.logoutUrl;
  public readonly isExpanded: WritableSignal<boolean> = signal(false);

  collapse() {
    this.isExpanded.set(false);
  }

  toggle() {
    this.isExpanded.update(expanded => !expanded);
  }
}
