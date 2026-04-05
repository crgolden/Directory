import { Component, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { AuthService, Session } from '../auth/auth.service';
import { Signal } from '@angular/core';

@Component({
  selector: 'app-user-session',
  imports: [],
  templateUrl: './user-session.component.html',
  styleUrl: './user-session.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserSessionComponent implements OnInit {

  private readonly authService = inject(AuthService);
  private readonly titleService = inject(Title);

  ngOnInit(): void {
    this.titleService.setTitle('Experience | User Session');
  }

  public readonly isAuthenticated = this.authService.isAuthenticated;
  public readonly isAnonymous = this.authService.isAnonymous;
  public readonly claims = this.authService.session;
}
