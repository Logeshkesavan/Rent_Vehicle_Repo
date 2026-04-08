# Rent Vehicle Database Project

SQL Server Database Project (.sqlproj) for version control of database schema and objects.

## Folder Structure

```
Rent_Vehicle_Database/
├── dbo/
│   ├── Tables/              # Place all table definitions here (auto-imported from existing DB)
│   ├── Stored Procedures/   # Place all stored procedures here (auto-imported from existing DB)
│   └── Views/               # Place all view definitions here (auto-imported from existing DB)
├── Scripts/
│   ├── Script.PreDeployment.sql   # Runs BEFORE deployment (constraints, drops, backups)
│   └── Script.PostDeployment.sql  # Runs AFTER deployment (seed data, FK re-enable)
├── Rent_Vehicle_Database.sqlproj  # Project file
└── README.md
```

## Workflow

### Step 1: Import Existing Database
In Visual Studio:
1. Right-click **Rent_Vehicle_Database** project
2. Select **Import** → **Database**
3. Connect to your existing `VehicleRentalDB` database
4. Select all Tables, Views, Stored Procedures, Functions
5. Click **Finish**

This will generate `.sql` files for each object in their respective folders.

### Step 2: Add to Git
```bash
git add Rent_Vehicle_Database/
git commit -m "feat: add SQL Server database project with existing schema"
git push origin develop
```

### Step 3: Make Schema Changes
- Modify tables in Visual Studio designer, OR
- Edit `.sql` files directly
- Right-click project → **Publish** to apply changes to local/test database
- Commit changes to Git for code review

### Step 4: Deploy to Environments
Use **Publish Profile** to deploy to different environments:

**From Visual Studio:**
- Right-click project → **Publish** → Select target server/database

**From Command Line:**
```powershell
sqlpackage.exe /Action:Publish `
  /SourceFile:Rent_Vehicle_Database/bin/Release/Rent_Vehicle_Database.dacpac `
  /TargetConnectionString:"Server=prod-server;Database=VehicleRentalDB;User Id=sa;Password=..." `
  /AllowIncompatiblePlatform
```

**From CI/CD Pipeline:**
```yaml
- name: Deploy Database
  run: |
    sqlpackage.exe /Action:Publish `
      /SourceFile:Rent_Vehicle_Database/bin/Release/Rent_Vehicle_Database.dacpac `
      /TargetConnectionString:"${{ secrets.PROD_DB_CONNECTION_STRING }}" `
      /AllowIncompatiblePlatform
```

## Key Features

✓ **Version Control**: All schema changes tracked in Git  
✓ **Code Review**: PR process for database changes  
✓ **Automated Deployment**: Deploy to any environment via pipeline  
✓ **Pre/Post Scripts**: Seed data, migrations, constraint management  
✓ **Diff Detection**: Visual Studio shows exact changes before deploy  

## Next Steps

1. Open Visual Studio
2. Right-click the database project → **Import Database**
3. Select your existing `VehicleRentalDB`
4. Commit the generated files to Git
5. Create a publish profile for each environment (Dev, Test, Prod)
