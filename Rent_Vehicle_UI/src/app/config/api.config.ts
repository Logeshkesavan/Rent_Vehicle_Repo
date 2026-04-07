import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ApiConfig {
  private readonly baseUrl = environment.apiBaseUrl;

  getBaseUrl(): string {
    return this.baseUrl;
  }

  getAuthUrl(): string {
    return `${this.baseUrl}/auth`;
  }

  getBookingsUrl(): string {
    return `${this.baseUrl}/bookings`;
  }

  getVehiclesUrl(): string {
    return `${this.baseUrl}/vehicles`;
  }

  getDashboardUrl(): string {
    return `${this.baseUrl}/dashboard`;
  }
}
