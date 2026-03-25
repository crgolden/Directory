import { Component, computed, inject, Signal, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { Title } from '@angular/platform-browser';
import {AuthService, Session} from '../auth/auth.service';

@Component({
  selector: 'app-user-session',
  imports: [],
  templateUrl: './user-session.component.html',
  styleUrl: './user-session.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserSessionComponent implements OnInit {

  private readonly auth = inject(AuthService);
  private readonly titleService = inject(Title);

  ngOnInit(): void {
    this.titleService.setTitle('Experience | User Session');
  }
  
  public session: Signal<Session> = this.auth.session;
  public isAuthenticated = this.auth.isAuthenticated;
  public isAnonymous = this.auth.isAnonymous;
  public claims = computed(() => this.session() || []);

}
