using NoteLab.Application;
using NoteLab.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using System.Text;
using Microsoft.IdentityModel.Tokens;


string connectionString = "Host=localhost;Port=5434;Database=notelab_db;Username=postgres;Password=admin";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<INoteRepository, NoteRepository>();
builder.Services.AddDbContext<NoteDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddIdentityCore<IdentityUser>().AddEntityFrameworkStores<NoteDbContext>().AddDefaultTokenProviders();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            // Adicione as 3 linhas abaixo:
            ValidIssuer = "NoteeApi",
            ValidAudience = "NoteeApp",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("SuperChaveSecretaDoNoteeComPeloMenos32Caracteres!"))
        };
    });

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityEndpoints();
app.MapNoteEndpoints();

app.Run();