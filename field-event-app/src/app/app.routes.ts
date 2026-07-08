import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'dispatcher',
    loadComponent: () => import('./features/dispatcher/dispatcher-dashboard/dispatcher-dashboard.component')
      .then(m => m.DispatcherDashboardComponent)
  },
  {
    path: 'technician',
    loadComponent: () => import('./features/technician/technician-dashboard/technician-dashboard.component')
      .then(m => m.TechnicianDashboardComponent)
  },
  // ניתוב ברירת מחדל
  { path: '', redirectTo: 'dispatcher', pathMatch: 'full' }
];