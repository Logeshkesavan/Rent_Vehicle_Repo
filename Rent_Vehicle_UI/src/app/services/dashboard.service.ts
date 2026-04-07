import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiConfig } from '../config/api.config';

export interface DashboardStats {
  totalBookings: number;
  activeRentals: number;
  totalRevenue: number;
  totalVehicles: number;
}

export interface RealTimeStats {
  todayPickups: number;
  todayReturns: number;
  vehiclesRented: number;
  vehiclesAvailable: number;
  pendingBookings: number;
  expectedRevenueToday: number;
}

export interface VehicleUtilization {
  vehicleId: number;
  vehicleName: string;
  category: string;
  totalBookings: number;
  daysBooked: number;
  utilizationPercentage: number;
  revenueGenerated: number;
}

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private apiUrl: string;

  constructor(private http: HttpClient, private apiConfig: ApiConfig) {
    this.apiUrl = this.apiConfig.getDashboardUrl();
  }

  getDashboardStats(): Observable<DashboardStats> {
    return this.http.get<DashboardStats>(`${this.apiUrl}/stats`);
  }

  getRealTimeStats(): Observable<RealTimeStats> {
    return this.http.get<RealTimeStats>(`${this.apiUrl}/realtime`);
  }

  getVehicleUtilization(): Observable<VehicleUtilization[]> {
    return this.http.get<VehicleUtilization[]>(`${this.apiUrl}/vehicle-utilization`);
  }

  getRevenueByCategory(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/revenue-by-category`);
  }

  getBookingsByStatus(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/bookings-by-status`);
  }
}
