import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login.component';
import { RegisterComponent } from './components/register/register.component';
import { VehiclesComponent } from './components/vehicles/vehicles.component';
import { BookingsComponent } from './components/bookings/bookings.component';
import { AdminDashboardComponent } from './components/admin-dashboard/admin-dashboard.component';
import { VehicleDetails } from './components/vehicle-details/vehicle-details';
import { BookingForm } from './components/booking-form/booking-form';
import { AdminVehicles } from './components/admin-vehicles/admin-vehicles';
import { AdminBookings } from './components/admin-bookings/admin-bookings';
import { authGuard, adminGuard, noAuthGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent, canActivate: [noAuthGuard] },
  { path: 'register', component: RegisterComponent, canActivate: [noAuthGuard] },
  { path: 'vehicles', component: VehiclesComponent, canActivate: [authGuard] },
  { path: 'vehicles/:id', component: VehicleDetails, canActivate: [authGuard] },
  { path: 'booking/:id', component: BookingForm, canActivate: [authGuard] },
  { path: 'bookings', component: BookingsComponent, canActivate: [authGuard] },
  { path: 'admin/dashboard', component: AdminDashboardComponent, canActivate: [adminGuard] },
  { path: 'admin/vehicles', component: AdminVehicles, canActivate: [adminGuard] },
  { path: 'admin/bookings', component: AdminBookings, canActivate: [adminGuard] },
  { path: '**', redirectTo: '/login' }
];
