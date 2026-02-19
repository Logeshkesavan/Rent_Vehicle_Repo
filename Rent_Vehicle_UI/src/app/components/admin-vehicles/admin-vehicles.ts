import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { VehicleService, Vehicle, CreateVehicleRequest } from '../../services/vehicle.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-admin-vehicles',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './admin-vehicles.html',
  styleUrl: './admin-vehicles.css',
})
export class AdminVehicles implements OnInit {
  vehicles = signal<Vehicle[]>([]);
  loading = signal(true);
  showModal = signal(false);
  isEditMode = signal(false);
  currentVehicleId = signal<number | null>(null);

  vehicleForm: CreateVehicleRequest = {
    category: 'Car',
    type: 'SUV',
    brand: '',
    model: '',
    year: new Date().getFullYear(),
    registrationNumber: '',
    fuelType: 'Petrol',
    transmission: 'Manual',
    seatingCapacity: 4,
    pricePerDay: 0,
    pricePerHour: 0,
    location: '',
    images: '',
    features: ''
  };

  errorMessage = signal('');
  successMessage = signal('');

  categories = ['Car', 'Bike', 'Scooter'];
  fuelTypes = ['Petrol', 'Diesel', 'Electric', 'Hybrid'];
  transmissions = ['Manual', 'Automatic'];

  constructor(
    private vehicleService: VehicleService,
    public authService: AuthService
  ) {}

  ngOnInit(): void {
    this.loadVehicles();
  }

  loadVehicles(): void {
    this.loading.set(true);
    this.vehicleService.getVehicles().subscribe({
      next: (data) => {
        this.vehicles.set(data);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error loading vehicles:', error);
        this.errorMessage.set('Failed to load vehicles');
        this.loading.set(false);
      }
    });
  }

  openCreateModal(): void {
    this.isEditMode.set(false);
    this.currentVehicleId.set(null);
    this.resetForm();
    this.showModal.set(true);
  }

  openEditModal(vehicle: Vehicle): void {
    this.isEditMode.set(true);
    this.currentVehicleId.set(vehicle.id);
    this.vehicleForm = {
      category: vehicle.category,
      type: vehicle.type,
      brand: vehicle.brand,
      model: vehicle.model,
      year: vehicle.year,
      registrationNumber: vehicle.registrationNumber,
      fuelType: vehicle.fuelType,
      transmission: vehicle.transmission || 'Manual',
      seatingCapacity: vehicle.seatingCapacity,
      pricePerDay: vehicle.pricePerDay,
      pricePerHour: vehicle.pricePerHour || 0,
      location: vehicle.location,
      images: vehicle.images || '',
      features: vehicle.features || ''
    };
    this.showModal.set(true);
  }

  closeModal(): void {
    this.showModal.set(false);
    this.resetForm();
    this.errorMessage.set('');
    this.successMessage.set('');
  }

  resetForm(): void {
    this.vehicleForm = {
      category: 'Car',
      type: 'SUV',
      brand: '',
      model: '',
      year: new Date().getFullYear(),
      registrationNumber: '',
      fuelType: 'Petrol',
      transmission: 'Manual',
      seatingCapacity: 4,
      pricePerDay: 0,
      pricePerHour: 0,
      location: '',
      images: '',
      features: ''
    };
  }

  submitForm(): void {
    this.errorMessage.set('');

    if (this.isEditMode()) {
      this.updateVehicle();
    } else {
      this.createVehicle();
    }
  }

  createVehicle(): void {
    this.vehicleService.createVehicle(this.vehicleForm).subscribe({
      next: (response) => {
        this.successMessage.set('Vehicle created successfully!');
        this.loadVehicles();
        setTimeout(() => {
          this.closeModal();
        }, 1500);
      },
      error: (error) => {
        console.error('Error creating vehicle:', error);
        this.errorMessage.set(error.error?.message || 'Failed to create vehicle');
      }
    });
  }

  updateVehicle(): void {
    const vehicleId = this.currentVehicleId();
    if (!vehicleId) return;

    this.vehicleService.updateVehicle(vehicleId, this.vehicleForm).subscribe({
      next: (response) => {
        this.successMessage.set('Vehicle updated successfully!');
        this.loadVehicles();
        setTimeout(() => {
          this.closeModal();
        }, 1500);
      },
      error: (error) => {
        console.error('Error updating vehicle:', error);
        this.errorMessage.set(error.error?.message || 'Failed to update vehicle');
      }
    });
  }

  deleteVehicle(id: number, vehicleName: string): void {
    if (confirm(`Are you sure you want to delete ${vehicleName}?`)) {
      this.vehicleService.deleteVehicle(id).subscribe({
        next: () => {
          this.successMessage.set('Vehicle deleted successfully!');
          this.loadVehicles();
          setTimeout(() => {
            this.successMessage.set('');
          }, 3000);
        },
        error: (error) => {
          console.error('Error deleting vehicle:', error);
          this.errorMessage.set(error.error?.message || 'Failed to delete vehicle');
          setTimeout(() => {
            this.errorMessage.set('');
          }, 3000);
        }
      });
    }
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
