import { AfterViewInit, Component, inject, OnInit, ChangeDetectionStrategy, ViewChild, ElementRef, HostListener } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { DomSanitizer, SafeResourceUrl, Title } from '@angular/platform-browser';
import { NavMenuComponent } from '../nav-menu/nav-menu.component';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, NavMenuComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(window:message)': 'onMessage($event)'
  }
})
export class AppComponent implements AfterViewInit, OnInit {

  private authService: AuthService = inject(AuthService);
  private titleService: Title = inject(Title);
  private sanitizer: DomSanitizer = inject(DomSanitizer);
  public iframeUrl: SafeResourceUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.authService.silentLoginUrl);
  public iframeVisible: boolean = false;

  ngOnInit(): void {
    this.titleService.setTitle('Experience | Home');
  }
  
  ngAfterViewInit(): void {
    if (this.authService.session() !== null) {
      return;
    }

    const loginUrl = `${this.authService.loginUrl}?prompt=none`;
    this.iframeUrl = this.sanitizer.bypassSecurityTrustResourceUrl(loginUrl);
    this.iframeVisible = true;
  }

  onMessage(event: MessageEvent) {
    if (event.data !== null && event.data['source'] == 'bff-silent-login') {
      console.log('Message received from iframe:', event.data);
      this.iframeVisible = false;
    }
  }

}
