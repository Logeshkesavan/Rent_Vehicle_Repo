import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiConfig } from '../config/api.config';

export interface Vehicle {
  id: number;
  category: string;
  type: string;
  brand: string;
  model: string;
  year: number;
  registrationNumber: string;
  fuelType: string;
  transmission?: string;
  seatingCapacity: number;
  pricePerDay: number;
  pricePerHour?: number;
  location: string;
  availabilityStatus: string;
  images?: string;
  features?: string;

  // Optional availability warnings (returned for date-range searches)
  hasPendingConflicts?: boolean;
  message?: string;
}

export interface VehicleDetail extends Vehicle {
  isAvailableToday: boolean;
  nextAvailableDate?: Date;
  upcomingBookings: BookingSummary[];
}

export interface BookingSummary {
  id: number;
  pickupDate: Date;
  returnDate: Date;
  status: string;
}

export interface VehicleAvailability {
  vehicleId: number;
  vehicleName: string;
  currentStatus: string;
  fromDate: Date;
  toDate: Date;
  totalDays: number;
  availableDays: number;
  bookedDays: number;
  dateAvailability: DateAvailability[];
}

export interface DateAvailability {
  date: Date;
  isAvailable: boolean;
  status: string;
}

export interface CreateVehicleRequest {
  category: string;
  type: string;
  brand: string;
  model: string;
  year: number;
  registrationNumber: string;
  fuelType: string;
  transmission?: string;
  seatingCapacity: number;
  pricePerDay: number;
  pricePerHour?: number;
  location: string;
  images?: string;
  features?: string;
}

@Injectable({
  providedIn: 'root'
})
export class VehicleService {
  private apiUrl: string;

  constructor(private http: HttpClient, private apiConfig: ApiConfig) {
    this.apiUrl = this.apiConfig.getVehiclesUrl();
  }

  getVehicles(category?: string, fromDate?: Date, toDate?: Date): Observable<Vehicle[]> {
    let url = this.apiUrl;
    const params = new URLSearchParams();

    if (category) params.append('category', category);
    if (fromDate) params.append('fromDate', fromDate.toISOString());
    if (toDate) params.append('toDate', toDate.toISOString());

    if (params.toString()) {
      url += '?' + params.toString();
    }

    return this.http.get<Vehicle[]>(url);
  }

  getVehicleById(id: number): Observable<VehicleDetail> {
    return this.http.get<VehicleDetail>(`${this.apiUrl}/${id}`);
  }

  getVehicleAvailability(id: number, fromDate?: Date, toDate?: Date): Observable<VehicleAvailability> {
    let url = `${this.apiUrl}/${id}/availability`;
    const params = new URLSearchParams();

    if (fromDate) params.append('fromDate', fromDate.toISOString());
    if (toDate) params.append('toDate', toDate.toISOString());

    if (params.toString()) {
      url += '?' + params.toString();
    }

    return this.http.get<VehicleAvailability>(url);
  }

  createVehicle(vehicle: CreateVehicleRequest): Observable<Vehicle> {
    return this.http.post<Vehicle>(this.apiUrl, vehicle);
  }

  updateVehicle(id: number, vehicle: CreateVehicleRequest): Observable<any> {
    return this.http.put(`${this.apiUrl}/${id}`, vehicle);
  }

  deleteVehicle(id: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${id}`);
  }
}
