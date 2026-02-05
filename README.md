### JewelryMS - Jewelry Management System (V1)
JewelryMS is a robust, multi-tenant enterprise solution designed to manage jewelry sales, inventory, and staff operations. This project implements a clean architecture with a focus on dynamic pricing logic and secure identity management.

## 🚀 Key Features Implemented
# 1. Advanced Sales & Pricing Logic
Dynamic Rate Calculation: Automatically calculates product prices based on live Gold/Silver market rates.

Multi-Level Discounts: Supports both fixed-amount and percentage-based discounts at the transaction level.

Tax & VAT Automation: Integrated tax logic tailored to specific jewelry categories.

# 2. Secure Identity & Access Management (IAM)
BCrypt Hashing: Industry-standard password hashing implemented in the application layer to ensure data integrity and prevent SQL-level encoding issues.

JWT Multi-Tenancy: Authentication tokens carry shop_id and role claims, ensuring strict data isolation between different jewelry shops.

Identity Recovery Flow: A complete "Forgot Password" system utilizing GUID-based reset tokens and expiration logic.

Console-Based Email Service: A custom-built debugging email service that logs reset links to the console for cost-free development and testing.

# 3. Technical Architecture
Backend: ASP.NET Core 9.0 Web API.

Database: PostgreSQL with Dapper ORM for high-performance data access.

Middleware: Custom global exception handling and authorization filters.

Time Management: Standardized on TIMESTAMPTZ and UTC to ensure consistent operations across different time zones (e.g., Bangladesh Standard Time).

### 🛠️ Tech Stack
Language: C#

Framework: .NET 9.0

ORM: Dapper

Database: PostgreSQL

Security: JWT, BCrypt.Net-Next

Logging: Microsoft.Extensions.Logging

## 📂 Project Structure
JewelryMS.Domain: Entities, Interfaces, and DTOs.

JewelryMS.Application: Business logic, Services, and Mappings.

JewelryMS.Infrastructure: Data access, Repositories, and Database migrations.

JewelryMS.API: Controllers, Middleware, and Configurations.

## 🏁 Getting Started
Prerequisites
.NET 9.0 SDK

PostgreSQL Instance

Installation
Clone the repository:

Bash
git clone https://github.com/Showrav88/JewelryMSV1.git
Update the connection string in appsettings.json.

Run the database migrations/scripts.

Start the application:

Bash
dotnet run --project src/JewelryMS.API
## 📝 Recent Updates (Feb 2026)
Resolved password hashing character corruption issues.

Implemented IAuthService with full password reset lifecycle.

Synchronized server-database time offsets for Bangladesh region.

Developed with ❤️ by Showrav
