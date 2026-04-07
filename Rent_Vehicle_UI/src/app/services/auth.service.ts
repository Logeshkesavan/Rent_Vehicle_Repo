import { Injectable, signal, PLATFORM_ID, inject, OnDestroy } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, throwError, interval, Subscription } from 'rxjs';
import { of } from 'rxjs';
import { ApiConfig } from '../config/api.config';

export interface User {
  id: number;
  name: string;
  email: string;
  phone: string;
  role: string;
}

export interface AuthResponse {
  success: boolean;
  message: string;
  user?: User;
  token?: string;
  refreshToken?: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService implements OnDestroy {
  private apiUrl: string;
  public currentUser = signal<User | null>(null);
  public isAuthenticated = signal<boolean>(false);
  private platformId = inject(PLATFORM_ID);
  private isBrowser = isPlatformBrowser(this.platformId);
  
  // Token expiry tracking
  private tokenExpiry: Date | null = null;
  private refreshSubscription: Subscription | null = null;
  
  // Time before expiry to trigger silent refresh (in milliseconds)
  // This is 1 minute before the token expires (token expires in 2 minutes)
  private readonly refreshThreshold = 60 * 1000; 

  constructor(private http: HttpClient, private router: Router, private apiConfig: ApiConfig) {
    this.apiUrl = this.apiConfig.getAuthUrl();
    this.checkAuthStatus();
  }

  ngOnDestroy(): void {
    if (this.refreshSubscription) {
      this.refreshSubscription.unsubscribe();
    }
  }

  private checkAuthStatus(): void {
    if (!this.isBrowser) return;

    const token = localStorage.getItem('token');
    const refreshToken = localStorage.getItem('refreshToken');
    const user = localStorage.getItem('user');

    if (token && refreshToken && user) {
      this.isAuthenticated.set(true);
      this.currentUser.set(JSON.parse(user));
      this.tokenExpiry = this.decodeTokenExpiry(token);
      this.startTokenRefreshTimer();
    }
  }

  /**
   * Starts the timer for automatic token refresh
   */
  private startTokenRefreshTimer(): void {
    if (this.refreshSubscription) {
      this.refreshSubscription.unsubscribe();
    }

    // Check token expiry every 30 seconds
    this.refreshSubscription = interval(30000).subscribe(() => {
      if (this.isAuthenticated() && this.tokenExpiry) {
        const timeUntilExpiry = this.tokenExpiry.getTime() - Date.now();
        
        // If token expires within the threshold, trigger silent refresh
        if (timeUntilExpiry > 0 && timeUntilExpiry <= this.refreshThreshold) {
          console.log('Token expiring soon, triggering silent refresh...');
          this.silentRefresh().subscribe({
            next: () => console.log('Silent refresh successful'),
            error: (err) => console.error('Silent refresh failed:', err)
          });
        }
      }
    });
  }

  /**
   * Decodes a JWT token to get its expiry time
   */
  private decodeTokenExpiry(token: string): Date | null {
    try {
      const payload = token.split('.')[1];
      const decoded = JSON.parse(atob(payload));
      
      if (decoded.exp) {
        return new Date(decoded.exp * 1000);
      }
    } catch (e) {
      console.error('Error decoding token:', e);
    }
    return null;
  }

  /**
   * Updates the token and starts refresh timer
   */
  private updateToken(token: string): void {
    if (this.isBrowser) {
      localStorage.setItem('token', token);
      this.tokenExpiry = this.decodeTokenExpiry(token);
      this.startTokenRefreshTimer();
    }
  }

  register(name: string, email: string, password: string, phone?: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/register`, {
      name,
      email,
      password,
      phone
    }).pipe(
      tap((response: AuthResponse) => {
        if (response.success && response.token && response.user) {
          if (this.isBrowser) {
            localStorage.setItem('token', response.token);
            localStorage.setItem('refreshToken', response.refreshToken || '');
            localStorage.setItem('user', JSON.stringify(response.user));
          }
          this.currentUser.set(response.user);
          this.isAuthenticated.set(true);
          this.tokenExpiry = this.decodeTokenExpiry(response.token);
          this.startTokenRefreshTimer();
        }
      }),
      catchError(error => {
        console.error('Registration error:', error);
        return of({
          success: false,
          message: error.error?.message || 'Registration failed'
        });
      })
    );
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, {
      email,
      password
    }).pipe(
      tap((response: AuthResponse) => {
        console.log('[AuthService] Login response received:', { success: response.success, hasToken: !!response.token, hasUser: !!response.user });
        console.log('[AuthService] isBrowser check:', this.isBrowser);
        
        if (response.success && response.token && response.user) {
          if (this.isBrowser) {
            console.log('[AuthService] Saving to localStorage...');
            localStorage.setItem('token', response.token);
            localStorage.setItem('refreshToken', response.refreshToken || '');
            localStorage.setItem('user', JSON.stringify(response.user));
            console.log('[AuthService] Saved successfully. Token length:', response.token.length);
            
            // Verify it was saved
            const savedToken = localStorage.getItem('token');
            console.log('[AuthService] Verification - Token in storage:', !!savedToken);
          } else {
            console.warn('[AuthService] NOT BROWSER - localStorage skipped!');
          }
          this.currentUser.set(response.user);
          this.isAuthenticated.set(true);
          this.tokenExpiry = this.decodeTokenExpiry(response.token);
          this.startTokenRefreshTimer();
        } else {
          console.warn('[AuthService] Login unsuccessful or missing data:', response);
        }
      }),
      catchError(error => {
        console.error('[AuthService] Login error:', error);
        return of({
          success: false,
          message: error.error?.message || 'Login failed'
        });
      })
    );
  }

  logout(): Observable<AuthResponse> {
    // Call logout endpoint to revoke all refresh tokens
    return this.http.post<AuthResponse>(`${this.apiUrl}/logout`, {}).pipe(
      tap(() => {
        this.clearAuthData();
      }),
      catchError(error => {
        // Even if logout fails on backend, clear client-side data
        this.clearAuthData();
        return of({
          success: true,
          message: 'Logged out'
        });
      })
    );
  }

  private clearAuthData(): void {
    if (this.isBrowser) {
      localStorage.removeItem('token');
      localStorage.removeItem('refreshToken');
      localStorage.removeItem('user');
    }
    this.currentUser.set(null);
    this.isAuthenticated.set(false);
    this.tokenExpiry = null;
    
    if (this.refreshSubscription) {
      this.refreshSubscription.unsubscribe();
      this.refreshSubscription = null;
    }
    
    this.router.navigate(['/login']);
  }

  /**
   * Silent refresh - refreshes token without user interaction
   */
  silentRefresh(): Observable<AuthResponse> {
    const token = this.isBrowser ? localStorage.getItem('token') : null;
    const refreshToken = this.isBrowser ? localStorage.getItem('refreshToken') : null;

    console.log('[AuthService] silentRefresh called - isBrowser:', this.isBrowser, 'hasToken:', !!token);

    if (!token || !refreshToken) {
      console.error('[AuthService] Silent refresh failed: No tokens available');
      return throwError(() => new Error('No tokens available'));
    }

    console.log('[AuthService] Calling refresh-token API (silent refresh)...');
    return this.http.post<AuthResponse>(`${this.apiUrl}/refresh-token`, {
      token,
      refreshToken
    }).pipe(
      tap((response: AuthResponse) => {
        console.log('[AuthService] Silent refresh response:', { success: response.success, hasToken: !!response.token });
        if (response.success && response.token) {
          this.updateToken(response.token);
          if (response.refreshToken) {
            localStorage.setItem('refreshToken', response.refreshToken);
          }
          if (response.user) {
            localStorage.setItem('user', JSON.stringify(response.user));
            this.currentUser.set(response.user);
          }
          console.log('[AuthService] Silent refresh successful');
        }
      }),
      catchError(error => {
        console.error('[AuthService] Silent refresh error:', error);
        // Silent refresh failed, logout and force re-authentication
        this.clearAuthData();
        return throwError(() => error);
      })
    );
  }

  /**
   * Manual refresh token - used by interceptor when 401 occurs
   */
  refreshToken(): Observable<AuthResponse> {
    const token = this.isBrowser ? localStorage.getItem('token') : null;
    const refreshToken = this.isBrowser ? localStorage.getItem('refreshToken') : null;

    console.log('[AuthService] refreshToken called - isBrowser:', this.isBrowser, 'hasToken:', !!token);

    if (!token || !refreshToken) {
      console.error('[AuthService] No tokens available for refresh');
      return throwError(() => new Error('No tokens available'));
    }

    console.log('[AuthService] Calling refresh-token API...');
    return this.http.post<AuthResponse>(`${this.apiUrl}/refresh-token`, {
      token,
      refreshToken
    }).pipe(
      tap((response: AuthResponse) => {
        console.log('[AuthService] Refresh response:', { success: response.success, hasToken: !!response.token });
        if (response.success && response.token) {
          this.updateToken(response.token);
          if (response.refreshToken) {
            localStorage.setItem('refreshToken', response.refreshToken);
          }
          if (response.user) {
            localStorage.setItem('user', JSON.stringify(response.user));
            this.currentUser.set(response.user);
          }
          console.log('[AuthService] Token refreshed successfully');
        }
      }),
      catchError(error => {
        console.error('[AuthService] Refresh failed:', error);
        // Token refresh failed, logout and force re-authentication
        this.clearAuthData();
        return throwError(() => error);
      })
    );
  }

  getToken(): string | null {
    return this.isBrowser ? localStorage.getItem('token') : null;
  }

  getRefreshToken(): string | null {
    return this.isBrowser ? localStorage.getItem('refreshToken') : null;
  }

  /**
   * Get remaining time until token expires (in milliseconds)
   */
  getTimeUntilExpiry(): number | null {
    if (!this.tokenExpiry) return null;
    return this.tokenExpiry.getTime() - Date.now();
  }

  /**
   * Check if token is expiring soon (within refresh threshold)
   */
  isTokenExpiringSoon(): boolean {
    const timeUntilExpiry = this.getTimeUntilExpiry();
    return timeUntilExpiry !== null && timeUntilExpiry > 0 && timeUntilExpiry <= this.refreshThreshold;
  }

  isAdmin(): boolean {
    return this.currentUser()?.role === 'Admin';
  }
}
