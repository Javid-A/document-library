using Document_library.DAL;
using Microsoft.EntityFrameworkCore;
using Amazon.S3;
using Amazon.Runtime;
using Document_library.Services.Implementations;
using Document_library.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Document_library.DAL.Entities;
using FluentValidation.AspNetCore;
using Document_library.DTOs;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.  

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle  

builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());

var awsCredentials = new BasicAWSCredentials(builder.Configuration["AWS:AccessKey"], builder.Configuration["AWS:SecretKey"]);
var s3client = new AmazonS3Client(awsCredentials, Amazon.RegionEndpoint.EUNorth1);
builder.Services.AddSingleton<IAmazonS3>(s3client);

builder.Services.AddFluentValidationAutoValidation()
                .AddFluentValidationClientsideAdapters()
                .AddValidatorsFromAssemblyContaining<RegisterDTOValidator>();

builder.Services.AddDbContext<DocumentDB>(opt => opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddIdentity<User, IdentityRole>().AddEntityFrameworkStores<DocumentDB>().AddDefaultTokenProviders();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IS3Service, S3Service>();
//builder.Services.AddSwaggerGen(c =>  
//{  
//    c.SwaggerDoc("v1", new() { Title = "Document library", Version = "v1" });  
//});  

var app = builder.Build();

// Configure the HTTP request pipeline.  
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
