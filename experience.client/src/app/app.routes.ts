import { Routes } from '@angular/router';
import { UserSessionComponent } from '../user-session/user-session.component';

export const routes: Routes = [
  {
    path: '',
    redirectTo: '/user-session',
    pathMatch: 'full'
  },
  {
    path: 'user-session',
    component: UserSessionComponent
  },
];
