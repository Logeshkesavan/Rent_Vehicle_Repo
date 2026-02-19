import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { VehicleService, Vehicle } from '../../services/vehicle.service';
import { BookingService, CreateBookingRequest } from '../../services/booking.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-booking-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './booking-form.html',
  styleUrl: './booking-form.css',
})
export class BookingForm implements OnInit {
  vehicle = signal<Vehicle | null>(null);
  pickupDate: string = '';
  returnDate: string = '';
  totalDays = signal(0);
  rentalAmount = signal(0);
  securityDeposit = signal(0);
  totalAmount = signal(0);
  submitting = signal(false);
  errorMessage = signal('');
  successMessage = signal('');
  minDate: string = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private vehicleService: VehicleService,
    private bookingService: BookingService,
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

    // Handle query parameters for pre-selected dates
    // Store dates but wait for vehicle to load before calculating
    this.route.queryParams.subscribe(params => {
      if (params['pickupDate']) {
        this.pickupDate = params['pickupDate'];
      }
      if (params['returnDate']) {
        this.returnDate = params['returnDate'];
      }
      // Don't call onDateChange here - wait for vehicle to load
    });
  }

  loadVehicle(id: number): void {
    this.vehicleService.getVehicleById(id).subscribe({
      next: (data) => {
        this.vehicle.set(data);
        // Calculate amounts if dates were pre-selected from query params
        if (this.pickupDate && this.returnDate) {
          this.onDateChange();
        }
      },
      error: (error) => {
        console.error('Error loading vehicle:', error);
        this.errorMessage.set('Failed to load vehicle details');
      }
    });
  }

  onDateChange(): void {
    if (this.pickupDate && this.returnDate) {
      const pickup = new Date(this.pickupDate);
      const returnD = new Date(this.returnDate);
      
      if (returnD <= pickup) {
        this.errorMessage.set('Return date must be after pickup date');
        return;
      }

      const diffTime = Math.abs(returnD.getTime() - pickup.getTime());
      const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
      
      this.totalDays.set(diffDays);
      
      const vehicle = this.vehicle();
      if (vehicle) {
        const rental = diffDays * vehicle.pricePerDay;
        const deposit = vehicle.pricePerDay * 2; // Security deposit = 2 days rent
        
        this.rentalAmount.set(rental);
        this.securityDeposit.set(deposit);
        this.totalAmount.set(rental + deposit);
      }
      
      this.errorMessage.set('');
    }
  }

  submitBooking(): void {
    if (!this.pickupDate || !this.returnDate) {
      this.errorMessage.set('Please select pickup and return dates');
      return;
    }

    if (!this.vehicle()) {
      this.errorMessage.set('Vehicle information not available');
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    const bookingRequest: CreateBookingRequest = {
      vehicleId: this.vehicle()!.id,
      pickupDate: new Date(this.pickupDate),
      returnDate: new Date(this.returnDate)
    };

    this.bookingService.createBooking(bookingRequest).subscribe({
      next: (response) => {
        this.successMessage.set('Booking created successfully!');
        setTimeout(() => {
          this.router.navigate(['/bookings']);
        }, 2000);
      },
      error: (error) => {
        console.error('Error creating booking:', error);
        this.errorMessage.set(error.error?.message || 'Failed to create booking. Please try again.');
        this.submitting.set(false);
      }
    });
  }

  goBack(): void {
    const vehicleId = this.vehicle()?.id;
    if (vehicleId) {
      this.router.navigate(['/vehicles', vehicleId]);
    } else {
      this.router.navigate(['/vehicles']);
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
