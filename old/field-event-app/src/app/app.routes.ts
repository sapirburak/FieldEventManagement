import { Routes } from '@angular/router';
import { LoginComponent } from './features/login/login.component';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
    {
        path: 'login',
        loadComponent: () => import('./features/login/login.component')
            .then(m => m.LoginComponent)
    
    },
    {
        path: 'dispatcher',
        loadComponent: () => import('./features/dispatcher/dispatcher-dashboard/dispatcher-dashboard.component')
            .then(m => m.DispatcherDashboardComponent),
        canActivate: [authGuard]
    },
    {
        path: 'technician',
        loadComponent: () => import('./features/technician/technician-dashboard/technician-dashboard.component')
            .then(m => m.TechnicianDashboardComponent),
        canActivate: [authGuard]
    },
    // ניתוב ברירת מחדל
    { path: '', redirectTo: 'login', pathMatch: 'full' }
];