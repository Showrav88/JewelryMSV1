🛠️ Project Summary: The Last 3 Days
## 1. Sales Logic & Dynamic Calculations
We moved beyond simple sales to a Product-Material-Rate architecture.

Gold/Silver Rates: Implemented logic to fetch the latest market rates so that product prices update dynamically.

Discount Logic: Added multi-level discount handling (percentage vs. fixed amount) at the point of sale.

Tax/Vat Integration: Calculations now include automated tax overheads based on jewelry categories.

## 2. User Authentication & "Suffocation" Fix
This was our biggest hurdle today. We solved the "poisoned hash" issue where manual SQL updates were breaking logins.

BCrypt Integration: Switched to C#-side hashing to ensure character encoding is 100% clean.

Identity Recovery: Created a full Forgot/Reset Password flow using unique GUID tokens.

Timezone Synchronization: Resolved the UTC vs. BD Local Time conflict by standardizing the database on TIMESTAMPTZ.

## 3. Security & Infrastructure
Custom Middleware: Implemented global exception handling and logging to catch database errors (like RLS violations) before they crash the app.

JWT Authorization: Enhanced the token payload to include shop_id and role, enabling Multi-Tenancy (Shop A cannot see Shop B's data).

Console Email Service: Built a non-paid EmailService that "sends" reset links to your terminal for zero-cost testing.

## 4. Repository & Architecture
Dapper & Npgsql: Optimized all Repository methods for high-performance PostgreSQL queries.

Clean Architecture: Separated concerns between Domain (Interfaces/Entities), Application (Logic/Services), and Infrastructure (Database).
