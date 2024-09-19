@echo off
echo Installing required NuGet packages...

cd "Document library"

dotnet add package AWSSDK.Extensions.NETCore.Setup --version 3.7.301
dotnet add package AWSSDK.S3 --version 3.7.402.9
dotnet add package DocumentFormat.OpenXml --version 3.1.0
dotnet add package FluentValidation --version 11.9.2
dotnet add package FluentValidation.AspNetCore --version 11.3.0
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.8
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 8.0.8
dotnet add package Microsoft.EntityFrameworkCore --version 8.0.8
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.8
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.8
dotnet add package Swashbuckle.AspNetCore --version 6.4.0
dotnet add package System.Drawing.Common --version 8.0.8

echo NuGet packages installed successfully.
pause
