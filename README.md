# 🎯 DominationPoint

A real-time territorial control game management system built with ASP.NET Core 9.0, featuring live game tracking, team management, and automated scoring.

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/tests-31%20E2E%20%2B%2050%2B%20unit-success)](tests)


## ✨ Features

### 🎮 Core Functionality
- **Live Game Management** - Real-time territorial control tracking
- **Control Point System** - Grid-based map with customizable control points
- **Team Management** - Create and manage teams with unique colors and codes
- **Automated Scoring** - Real-time score calculation based on control point ownership
- **Game Scheduling** - Schedule games with start/end times
- **Scoreboard** - View final rankings and scores after game completion

### 👥 User Roles
- **Admin** - Full system access, game creation, team management
- **Team** - Participate in games via mobile numpad authentication // client side not included / under development

### 🎨 UI Features
- Clean, responsive admin interface
- Grid-based map editor (10x10)
- Color-coded team visualization
- Real-time control point status updates

---

## 🛠 Technology Stack

### Backend
- **ASP.NET Core 9.0** - Web framework
- **Entity Framework Core 9.0** - ORM
- **ASP.NET Core Identity** - Authentication & Authorization
- **SQLite** - Database

### Frontend
- **Razor Pages** - Server-side rendering
- **Bootstrap 5** - UI framework
- **Vanilla JavaScript** - Interactive features

### Testing
- **xUnit** - Unit testing (50+ tests, 80%+ coverage)
- **Playwright** - E2E testing (31 comprehensive tests)
- **Moq** - Mocking framework
- **Coverlet** - Code coverage

### Architecture
- **Clean Architecture** - Domain-driven design
- **Repository Pattern** - Data access abstraction
- **Service Layer** - Business logic separation

---

## 🚀 Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)
- [Node.js](https://nodejs.org/) (for Playwright E2E tests)

### Installation
1. **Clone the repository**
2. **Restore dependencies**
3. **Update database**
4. **Run the application**
5. **Access the application**

- Navigate to: `https://localhost:7111`
- Default admin credentials:
  - **Email:** `admin@dominationpoint.com`
  - **Password:** `AdminP@ssw0rd!`

### First-Time Setup

The application will automatically:
- ✅ Create the SQLite database
- ✅ Seed admin user

---

## 🧪 Testing

### Unit Tests and Integration tests (91%+ Coverage, 507 tests) coverlet image will be found in the project's root directory as 'coverage.png'
### Playwright Tests (31 tests)
### Lighthouse Performance Report (83+ Scores) will be found in the project's root directory as 'lighthouse.png'

![Coverage](https://github.com/lullak/DominationPoint/blob/master/Coverage.png?raw=true "Coverage")
![Lighthouse](https://github.com/lullak/DominationPoint/blob/master/Lighthouse.png?raw=true "Lighthouse")
