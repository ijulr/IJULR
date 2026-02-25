# IJULR - International Journal of Unified Law Research

A complete .NET 8 MVC web application for managing an academic journal.

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK
- SQL Server (LocalDB, Express, or Full)
- Visual Studio 2022 or VS Code

### Setup Steps (Using SQL Script - Recommended)

1. **Extract the zip file**

2. **Create Database**
   - Open SQL Server Management Studio (SSMS)
   - Connect to your SQL Server
   - Open `Database/IJULR_Complete_Database.sql`
   - Execute the script (F5)

3. **Update Connection String** (if needed)
   Edit `IJULR.Web/appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Database=IJULR;Trusted_Connection=True;TrustServerCertificate=True"
   }
   ```

4. **Run the Application**
   ```bash
   cd IJULR.Web
   dotnet run
   ```

5. **Open in Browser**
   - Website: https://localhost:5001
   - Admin: https://localhost:5001/editorial/login
   - Reviewer: https://localhost:5001/reviewer/login

---

## 🔐 Login Credentials

| Portal | URL | Email | Password |
|--------|-----|-------|----------|
| Admin | /editorial/login | admin@ijulr.com | Admin@123 |
| Reviewer | /reviewer/login | reviewer1@example.com | Admin@123 |

---

## ✨ Features

- Paper submission with up to 3 authors
- Tracking ID system (IJULR-2026-0001)
- Double-blind peer review workflow
- Admin dashboard with statistics
- Reviewer management & assignment
- Volume/Issue management
- Payment verification
- DOI assignment & publication
- Citation export (BibTeX, RIS)
- Dynamic page content management

---

## 📊 Status Workflow

```
Submitted → Under Review → Revision → Accepted → Payment Pending → Published
                           ↓
                       Rejected
```

---

## 📁 Project Structure

```
IJULR/
├── Database/
│   └── IJULR_Complete_Database.sql
└── IJULR.Web/
    ├── Controllers/
    ├── Models/
    ├── Views/
    ├── Data/
    ├── Services/
    └── wwwroot/
```

---

© 2025 IJULR. All rights reserved.
