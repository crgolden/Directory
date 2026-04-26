import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HomeComponent implements OnInit {

  private readonly titleService = inject(Title);
  readonly authService = inject(AuthService);

  readonly benefits = [
    {
      icon: 'bi-shield-check',
      title: 'Insurance Claims',
      body: 'Serial numbers and purchase dates on hand when you need them most — speed up claims and maximize your payouts.'
    },
    {
      icon: 'bi-tools',
      title: 'Maintenance Scheduling',
      body: 'Track every appliance, vehicle, and device so you never miss a service interval or void a warranty.'
    },
    {
      icon: 'bi-file-earmark-text',
      title: 'Estate & Will Preparation',
      body: 'An organized inventory simplifies asset documentation and saves your loved ones hours of guesswork.'
    },
    {
      icon: 'bi-book',
      title: 'User Manual Access',
      body: 'AI-powered search locates any product manual instantly — no more hunting through boxes or manufacturer sites.'
    },
    {
      icon: 'bi-calendar2-check',
      title: 'Warranty Tracking',
      body: 'Know exactly what is covered, for how long, and when to act — before coverage lapses.'
    },
    {
      icon: 'bi-currency-dollar',
      title: 'Resale Value',
      body: 'Accurate records with serial numbers and service history boost buyer confidence and command higher prices.'
    }
  ];

  ngOnInit(): void {
    this.titleService.setTitle('Experience | Home');
  }
}
