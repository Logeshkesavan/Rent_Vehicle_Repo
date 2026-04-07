import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiConfig } from '../config/api.config';

export interface Booking {
  id: number;
  userId: number;
  vehicleId: number;
  vehicleName: string;
  pickupDate: Date;
  returnDate: Date;
  totalDays: number;
  rentalAmount: number;
  securityDeposit: number;
  totalAmount: number;
  status: string;
  paymentStatus: string;
}

export interface CreateBookingRequest {
  vehicleId: number;
  pickupDate: Date;
  returnDate: Date;
}

export interface UpdateBookingStatusRequest {
  status: string;
}

@Injectable({
  providedIn: 'root'
})
export class BookingService {
  private apiUrl: string;

  constructor(private http: HttpClient, private apiConfig: ApiConfig) {
    this.apiUrl = this.apiConfig.getBookingsUrl();
  }

  getBookings(): Observable<Booking[]> {
    return this.http.get<Booking[]>(this.apiUrl);
  }

  getBookingById(id: number): Observable<Booking> {
    return this.http.get<Booking>(`${this.apiUrl}/${id}`);
  }

  createBooking(booking: CreateBookingRequest): Observable<Booking> {
    return this.http.post<Booking>(this.apiUrl, booking);
  }

  updateBookingStatus(id: number, status: string): Observable<any> {
    return this.http.put(`${this.apiUrl}/${id}/status`, { status });
  }

  cancelBooking(id: number): Observable<any> {
    return this.http.post(`${this.apiUrl}/${id}/cancel`, {});
  }
}
