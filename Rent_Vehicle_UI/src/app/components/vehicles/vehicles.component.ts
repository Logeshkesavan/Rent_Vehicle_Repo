import { Component, signal, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { VehicleService, Vehicle } from '../../services/vehicle.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-vehicles',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './vehicles.component.html',
  styleUrl: './vehicles.component.css'
})
export class VehiclesComponent {
  vehicles = signal<Vehicle[]>([]);
  isLoading = signal(false);
  selectedCategory = '';
  fromDateString = '';
  toDateString = '';
  minDate = '';
  filteredVehicles = computed(() => {
    return this.vehicles();
  });

  constructor(
    private vehicleService: VehicleService,
    public authService: AuthService,
    private router: Router
  ) {
    const today = new Date();
    this.minDate = today.toISOString().split('T')[0];
    this.loadVehicles();
  }

  loadVehicles(): void {
    this.isLoading.set(true);

    const category = this.selectedCategory || undefined;
    const fromDate = this.fromDateString ? new Date(this.fromDateString) : undefined;
    const toDate = this.toDateString ? new Date(this.toDateString) : undefined;

    this.vehicleService.getVehicles(category, fromDate, toDate).subscribe({
      next: (data) => {
        this.vehicles.set(data);
        this.isLoading.set(false);
      },
      error: (error) => {
        console.error('Error loading vehicles:', error);
        this.isLoading.set(false);
      }
    });
  }

  bookVehicle(vehicleId: number): void {
    this.router.navigate(['/vehicles', vehicleId]);
  }

  logout(): void {
    this.authService.logout().subscribe({
      next: () => {
        // Logout successful, navigation handled in auth.service
      },
      error: (error) => {
        console.error('Logout error:', error);
        // Auth service handles redirect even on error
      }
    });
  }
}
