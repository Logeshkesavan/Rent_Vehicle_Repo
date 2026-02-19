import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { VehicleService, VehicleDetail, BookingSummary } from '../../services/vehicle.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-vehicle-details',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './vehicle-details.html',
  styleUrl: './vehicle-details.css',
})
export class VehicleDetails implements OnInit {
  vehicle = signal<VehicleDetail | null>(null);
  loading = signal(true);
  
  // Date selection for booking
  selectedFromDate = '';
  selectedToDate = '';
  dateError = signal('');
  bookingPossible = signal(false);

  minDate: string = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private vehicleService: VehicleService,
    public authService: AuthService
  ) {
    // Set minimum date to today
    const today = new Date();
    this.minDate = today.toISOString().split('T')[0];
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadVehicle(+id);
    }
  }

  loadVehicle(id: number): void {
    this.vehicleService.getVehicleById(id).subscribe({
      next: (data) => {
        this.vehicle.set(data);
        this.loading.set(false);
        
        // Set default dates
        if (data.isAvailableToday) {
          const today = new Date();
          const tomorrow = new Date(today);
          tomorrow.setDate(tomorrow.getDate() + 1);
          this.selectedFromDate = today.toISOString().split('T')[0];
          this.selectedToDate = tomorrow.toISOString().split('T')[0];
          this.checkBookingPossible();
        }
      },
      error: (error) => {
        console.error('Error loading vehicle:', error);
        this.loading.set(false);
      }
    });
  }

  onDateChange(): void {
    this.checkBookingPossible();
  }

  checkBookingPossible(): void {
    const vehicle = this.vehicle();
    if (!vehicle || !this.selectedFromDate || !this.selectedToDate) {
      this.bookingPossible.set(false);
      return;
    }

    const fromDate = new Date(this.selectedFromDate);
    const toDate = new Date(this.selectedToDate);

    if (toDate <= fromDate) {
      this.dateError.set('Return date must be after pickup date');
      this.bookingPossible.set(false);
      return;
    }

    // Check if selected dates overlap with any upcoming bookings
    const hasConflict = vehicle.upcomingBookings.some(booking => {
      const bookingStart = new Date(booking.pickupDate);
      const bookingEnd = new Date(booking.returnDate);
      return fromDate < bookingEnd && toDate > bookingStart;
    });

    if (hasConflict) {
      this.dateError.set('Vehicle is not available for the selected dates');
      this.bookingPossible.set(false);
    } else {
      this.dateError.set('');
      this.bookingPossible.set(true);
    }
  }

  bookVehicle(): void {
    const vehicleId = this.vehicle()?.id;
    if (vehicleId && this.bookingPossible()) {
      // Navigate to booking page with dates
      this.router.navigate(['/booking', vehicleId], {
        queryParams: {
          pickupDate: this.selectedFromDate,
          returnDate: this.selectedToDate
        }
      });
    }
  }

  goBack(): void {
    this.router.navigate(['/vehicles']);
  }

  getStatusClass(): string {
    const vehicle = this.vehicle();
    if (!vehicle) return 'badge-secondary';
    
    if (vehicle.isAvailableToday) {
      return 'badge-success';
    }
    return 'badge-warning';
  }

  getStatusText(): string {
    const vehicle = this.vehicle();
    if (!vehicle) return 'Unknown';
    
    if (vehicle.isAvailableToday) {
      return 'Available Today';
    }
    return `Next Available: ${this.formatDate(vehicle.nextAvailableDate)}`;
  }

  formatDate(date: Date | undefined): string {
    if (!date) return 'N/A';
    return new Date(date).toLocaleDateString('en-IN', {
      weekday: 'short',
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }

  isBookingConfirmedOrPickedUp(status: string): boolean {
    return status === 'Confirmed' || status === 'PickedUp';
  }

  logout(): void {
    this.authService.logout();
  }
}
