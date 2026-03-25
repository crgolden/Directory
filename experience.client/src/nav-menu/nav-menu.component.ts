import { Component, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'nav-menu',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './nav-menu.component.html',
  styleUrl: './nav-menu.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NavMenuComponent {
  private auth = inject(AuthService);
  public username = this.auth.username;
  public authenticated = this.auth.isAuthenticated;
  public anonymous = this.auth.isAnonymous;
  public logoutUrl = this.auth.logoutUrl;

  isExpanded = signal(false);

  collapse() {
    this.isExpanded.set(false);
  }

  toggle() {
    this.isExpanded.update(expanded => !expanded);
  }
}
