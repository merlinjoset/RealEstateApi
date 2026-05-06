# Jose For Land — Real Estate API

Backend API for the **Jose For Land** real estate platform serving Kanyakumari district, Tamil Nadu.

Built with .NET 8 Web API, Entity Framework Core, PostgreSQL, and JWT authentication.

## Stack

- **.NET 8** Web API (minimal hosting)
- **Entity Framework Core 8** + Npgsql (PostgreSQL provider)
- **JWT Bearer Authentication** + Refresh Tokens
- **BCrypt.Net-Next** for password hashing
- **Swagger / OpenAPI** for API documentation

## Project Structure

```
RealEstateApi/
├── Controllers/       # API endpoints
├── DTOs/              # Request / response models
├── Data/              # AppDbContext + EF configuration
├── Middleware/        # Custom middleware
├── Migrations/        # EF Core migrations
├── Models/            # Domain entities
├── Services/          # Business logic
├── Program.cs         # Application entry + DI setup
└── appsettings.json   # Configuration
```

## Domain Highlights

- **Property listings** with Indian-specific fields: area in cents, price in lakhs, road access, EC/Patta/Chitta legal status
- **Approval workflow** — client-submitted properties go through admin approval
- **Admin & user roles** with role-based authorization
- **Property documents** (EC, Patta, Layout, etc.) with public/private visibility
- **Inquiry management** with status tracking
- **Locations** focused on Kanyakumari: Nagercoil, Marthandam, Thuckalay, Kanyakumari, Colachel, Padmanabhapuram

## Getting Started

### Prerequisites

- .NET 8 SDK
- PostgreSQL 14+
- (Optional) `dotnet-ef` global tool: `dotnet tool install --global dotnet-ef`

### Setup

```bash
# Restore dependencies
dotnet restore

# Configure your DB connection in appsettings.Development.json
# "DefaultConnection": "Host=localhost;Database=joseforland_dev;Username=postgres;Password=..."

# Apply migrations
dotnet ef database update

# Run the API (hot reload)
dotnet watch run
```

The API will start on `https://localhost:5001` with Swagger UI at `/swagger`.

### Default Admin Account (seeded)

```
Email:    admin@joseforland.com
Password: Admin@123
```

> **Important:** Change the seeded password immediately in any non-development environment.

## API Endpoints

| Method | Endpoint                            | Auth   | Description                       |
| ------ | ----------------------------------- | ------ | --------------------------------- |
| POST   | `/api/auth/register`                | —      | Register a new user               |
| POST   | `/api/auth/login`                   | —      | Login, get access + refresh tokens |
| POST   | `/api/auth/refresh`                 | —      | Refresh access token              |
| POST   | `/api/auth/logout`                  | User   | Revoke refresh token              |
| GET    | `/api/auth/me`                      | User   | Current user profile              |
| GET    | `/api/properties`                   | —      | List properties (filters + paging) |
| GET    | `/api/properties/featured`          | —      | Featured listings                 |
| GET    | `/api/properties/{id}`              | —      | Property details                  |
| GET    | `/api/properties/{id}/related`      | —      | Related properties                |
| GET    | `/api/properties/pending`           | Admin  | Properties awaiting approval      |
| POST   | `/api/properties`                   | User   | Create property                   |
| PUT    | `/api/properties/{id}`              | Admin  | Update property                   |
| POST   | `/api/properties/{id}/approve`      | Admin  | Approve / reject property         |
| POST   | `/api/properties/{id}/favorite`     | User   | Toggle favorite                   |
| DELETE | `/api/properties/{id}`              | Admin  | Delete property                   |
| POST   | `/api/inquiries`                    | —      | Submit an inquiry (public)        |
| GET    | `/api/inquiries`                    | Admin  | List all inquiries                |
| PATCH  | `/api/inquiries/{id}/read`          | Admin  | Mark inquiry as read              |
| DELETE | `/api/inquiries/{id}`               | Admin  | Delete inquiry                    |

## Frontend

The companion React frontend lives at [RealEstateWeb](https://github.com/merlinjoset/RealEstateWeb) (separate repo).

## License

Proprietary — Jose For Land. All rights reserved.
