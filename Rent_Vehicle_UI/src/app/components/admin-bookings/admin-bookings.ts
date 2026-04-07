import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { BookingService, Booking } from '../../services/booking.service';
import { AuthService } from '../../services/auth.service';
import { ApiConfig } from '../../config/api.config';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

interface AdminBooking extends Booking {
  userName?: string;
  userEmail?: string;
}

@Component({
  selector: 'app-admin-bookings',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './admin-bookings.html',
  styleUrl: './admin-bookings.css',
})
export class AdminBookings implements OnInit {
  bookings = signal<AdminBooking[]>([]);
  filteredBookings = signal<AdminBooking[]>([]);
  loading = signal(true);
  selectedStatus = 'All';
  errorMessage = signal('');
  successMessage = signal('');

  statuses = ['All', 'Pending', 'Confirmed', 'PickedUp', 'Completed', 'Cancelled'];

  constructor(
    private http: HttpClient,
    private bookingService: BookingService,
    public authService: AuthService,
    private apiConfig: ApiConfig
  ) {}

  ngOnInit(): void {
    this.loadAllBookings();
  }

  loadAllBookings(): void {
    this.loading.set(true);
    // Using HttpClient directly - /api/bookings returns all bookings for admin
    this.http.get<AdminBooking[]>(this.apiConfig.getBookingsUrl()).subscribe({
      next: (data) => {
        this.bookings.set(data);
        this.filteredBookings.set(data);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error loading bookings:', error);
        this.errorMessage.set('Failed to load bookings');
        this.loading.set(false);
      }
    });
  }

  filterByStatus(): void {
    const all = this.bookings();
    if (this.selectedStatus === 'All') {
      this.filteredBookings.set(all);
    } else {
      this.filteredBookings.set(all.filter(b => b.status === this.selectedStatus));
    }
  }

  updateBookingStatus(bookingId: number, newStatus: string): void {
    if (confirm(`Are you sure you want to update this booking status to ${newStatus}?`)) {
      this.bookingService.updateBookingStatus(bookingId, newStatus).subscribe({
        next: () => {
          this.successMessage.set('Booking status updated successfully!');
          this.loadAllBookings();
          setTimeout(() => {
            this.successMessage.set('');
          }, 3000);
        },
        error: (error) => {
          console.error('Error updating booking status:', error);
          this.errorMessage.set(error.error?.message || 'Failed to update booking status');
          setTimeout(() => {
            this.errorMessage.set('');
          }, 3000);
        }
      });
    }
  }

  getStatusClass(status: string): string {
    switch(status) {
      case 'Confirmed':
        return 'badge-success';
      case 'PickedUp':
        return 'badge-info';
      case 'Completed':
        return 'badge-success';
      case 'Cancelled':
        return 'badge-danger';
      case 'Pending':
      default:
        return 'badge-warning';
    }
  }

  getPaymentStatusClass(status: string): string {
    return status === 'Paid' ? 'badge-success' : 'badge-warning';
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
