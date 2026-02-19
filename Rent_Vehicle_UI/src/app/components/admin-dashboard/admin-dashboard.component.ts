import { Component, signal, OnInit, ViewChild, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DashboardService, DashboardStats, RealTimeStats, VehicleUtilization } from '../../services/dashboard.service';
import { AuthService } from '../../services/auth.service';
import { BaseChartDirective, NgChartsModule } from 'ng2-charts';
import { ChartConfiguration } from 'chart.js';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, NgChartsModule],
  templateUrl: './admin-dashboard.component.html',
  styleUrl: './admin-dashboard.component.css'
})
export class AdminDashboardComponent implements OnInit, OnDestroy {
  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

  stats = signal<DashboardStats>({
    totalBookings: 0,
    activeRentals: 0,
    totalRevenue: 0,
    totalVehicles: 0
  });

  realTimeStats = signal<RealTimeStats>({
    todayPickups: 0,
    todayReturns: 0,
    vehiclesRented: 0,
    vehiclesAvailable: 0,
    pendingBookings: 0,
    expectedRevenueToday: 0
  });

  vehicleUtilization = signal<VehicleUtilization[]>([]);
  private refreshInterval: any;

  // Chart data
  public barChartOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: {
        grid: {
          display: false
        }
      },
      y: {
        beginAtZero: true,
        max: 100,
        ticks: {
          callback: function(value) {
            return value + '%';
          }
        }
      }
    },
    plugins: {
      legend: {
        display: false
      },
      tooltip: {
        callbacks: {
          label: function(context) {
            return 'Utilization: ' + context.parsed.y + '%';
          }
        }
      }
    }
  };

  public barChartData: ChartConfiguration<'bar'>['data'] = {
    labels: [],
    datasets: [
      {
        data: [],
        backgroundColor: [],
        borderColor: [],
        borderWidth: 1,
        borderRadius: 4
      }
    ]
  };

  // Revenue breakdown chart
  public pieChartOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'bottom'
      },
      tooltip: {
        callbacks: {
          label: function(context) {
            const label = context.label || '';
            const value = context.parsed || 0;
            return label + ': ₹' + value.toLocaleString();
          }
        }
      }
    }
  };

  public pieChartData: ChartConfiguration<'doughnut'>['data'] = {
    labels: [],
    datasets: [
      {
        data: [],
        backgroundColor: ['#667eea', '#48bb78', '#ed8936'],
        borderWidth: 2,
        borderColor: '#fff'
      }
    ]
  };

  revenueByCategory = signal<{category: string, revenue: number}[]>([]);

  constructor(
    private dashboardService: DashboardService,
    public authService: AuthService
  ) { }

  ngOnInit(): void {
    this.loadDashboardData();
    
    // Auto-refresh real-time stats every 30 min
    this.refreshInterval = setInterval(() => {
      this.loadRealTimeStats();
    }, 300000);
  }

  ngOnDestroy(): void {
    if (this.refreshInterval) {
      clearInterval(this.refreshInterval);
    }
  }

  loadDashboardData(): void {
    // Load basic stats
    this.dashboardService.getDashboardStats().subscribe({
      next: (data) => {
        this.stats.set(data);
      },
      error: (error) => console.error('Error loading stats:', error)
    });

    // Load real-time stats
    this.loadRealTimeStats();

    // Load vehicle utilization
    this.dashboardService.getVehicleUtilization().subscribe({
      next: (data) => {
        this.vehicleUtilization.set(data);
        this.updateChartData(data);
      },
      error: (error) => console.error('Error loading utilization:', error)
    });

    // Load revenue breakdown
    this.loadRevenueBreakdown();
  }

  loadRealTimeStats(): void {
    this.dashboardService.getRealTimeStats().subscribe({
      next: (data) => {
        this.realTimeStats.set(data);
      },
      error: (error) => console.error('Error loading real-time stats:', error)
    });
  }

  updateChartData(data: VehicleUtilization[]): void {
    const labels = data.map(v => v.vehicleName);
    const utilizationData = data.map(v => v.utilizationPercentage);
    const backgroundColors = data.map(v => {
      if (v.utilizationPercentage > 70) return 'rgba(72, 187, 120, 0.8)';
      if (v.utilizationPercentage >= 40) return 'rgba(237, 137, 54, 0.8)';
      return 'rgba(245, 101, 101, 0.8)';
    });
    const borderColors = data.map(v => {
      if (v.utilizationPercentage > 70) return 'rgb(72, 187, 120)';
      if (v.utilizationPercentage >= 40) return 'rgb(237, 137, 54)';
      return 'rgb(245, 101, 101)';
    });

    this.barChartData = {
      labels: labels,
      datasets: [
        {
          data: utilizationData,
          backgroundColor: backgroundColors,
          borderColor: borderColors,
          borderWidth: 1,
          borderRadius: 4
        }
      ]
    };
  }

  loadRevenueBreakdown(): void {
    this.dashboardService.getRevenueByCategory().subscribe({
      next: (data) => {
        this.revenueByCategory.set(data);
        this.pieChartData = {
          labels: data.map((d: any) => d.category),
          datasets: [
            {
              data: data.map((d: any) => d.revenue),
              backgroundColor: ['#667eea', '#48bb78', '#ed8936'],
              borderWidth: 2,
              borderColor: '#fff'
            }
          ]
        };
      },
      error: (error) => console.error('Error loading revenue breakdown:', error)
    });
  }

  getUtilizationClass(utilization: number): string {
    if (utilization > 70) return 'util-high';
    if (utilization >= 40) return 'util-medium';
    return 'util-low';
  }

  getUtilizationStatus(utilization: number): string {
    if (utilization > 70) return 'High';
    if (utilization >= 40) return 'Medium';
    return 'Low';
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
