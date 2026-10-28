using NoteLab.Application;
using NoteLab.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;


string connectionString = "Host=localhost;Port=5434;Database=notelab_db;Username=postgres;Password=admin";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<INoteRepository, NoteRepository>();
builder.Services.AddDbContext<NoteDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddIdentityCore<IdentityUser>().AddEntityFrameworkStores<NoteDbContext>();
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

app.MapGet("/notas", async (INoteRepository repo, ClaimsPrincipal user, CancellationToken ct, int page = 1, int pageSize = 10) =>
{
    string ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    List<NoteResponse> notesResponse = await repo.GetNotesAsync(ownerId, page, pageSize, ct);

    return Results.Ok(notesResponse);
}).RequireAuthorization();

app.MapGet("/notas/{id}", async (INoteRepository repo, ClaimsPrincipal user, CancellationToken ct, Guid id) =>
{
    string ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    Note note = await repo.GetByIdAsync(ownerId, id, ct);

    if (note == null)
    {
        return Results.NotFound();
    }

    NoteResponse notesResponse =  new NoteResponse(note.Id, note.Title.Value, note.Status.ToString());

    return Results.Ok(notesResponse);
});
// }).RequireAuthorization();

app.MapPut("/notas/{id}", async (INoteRepository repo, ClaimsPrincipal user, CancellationToken ct, Guid id, UpdateNoteRequest data) =>
{
    string ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    Note note = await repo.UpdateNoteAsync(ownerId, data.ExpectedVersion, id, data.Title, data.Content, ct);

    if (note == null)
    {
        return Results.NotFound();
    }

    NoteResponse notesResponse =  new NoteResponse(note.Id, note.Title.Value, note.Status.ToString());

    return Results.Ok(notesResponse);
});
// }).RequireAuthorization();

app.MapPost("/notas", async (CreateNoteRequest data, ClaimsPrincipal user, INoteRepository repo, CancellationToken ct) =>
{

    string ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    ownerId = "asderw2-23rwfea4-g65ewfew4-4twgfsd";

    Note note = await repo.AddNoteAsync(ownerId, new Note(new NoteTitle(data.Title), ownerId, data.Content), ct);

    NoteResponse noteResponse = new NoteResponse(note.Id, note.Title.Value, note.Status.ToString());

    return Results.Created($"/notas/{noteResponse.Id}", noteResponse);
});
// }).RequireAuthorization();

app.MapDelete("/notas/{id}", async (INoteRepository repo, ClaimsPrincipal user, CancellationToken ct, Guid id) =>
{

    string ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    bool isDeleted = await repo.DeleteNoteAsync(ownerId, id, ct);

    if (isDeleted == false)
    {
        return Results.NoContent();
    }

    return Results.Ok(isDeleted);
});
// }).RequireAuthorization();

app.MapPost("/auth/register", async (UserManager<IdentityUser> userManager, CancellationToken ct, RegisterRequest request) =>
{
    var user = new IdentityUser { UserName = request.Email, Email = request.Email };

    var result = await userManager.CreateAsync(user, request.Password);

    if (!result.Succeeded)
    {
        return Results.BadRequest(result.Errors);
    }

    return Results.Ok();
});

app.MapPost("/auth/login", async (UserManager<IdentityUser> userManager, CancellationToken ct, LoginrRequest request) =>
{

    var user = await userManager.FindByEmailAsync(request.Email);

    if (user == null || !await userManager.CheckPasswordAsync(user, request.Password))
    {
        return Results.Unauthorized();
    }

    // 1. Definimos o payload (as Claims)
    var claims = new[] 
    { 
        new Claim(ClaimTypes.NameIdentifier, user.Id) 
    };

    // 2. Definimos a chave e o algoritmo de assinatura
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("SuperChaveSecretaDoNoteeComPeloMenos32Caracteres!"));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    // 3. Montamos o token
    var token = new JwtSecurityToken(
        issuer: "NoteeApi",
        audience: "NoteeApp",
        claims: claims,
        expires: DateTime.UtcNow.AddHours(2),
        signingCredentials: creds
    );

    // 4. Serializamos para string
    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

    return Results.Ok(new { Token = tokenString });
});

app.MapPost("/auth/forgot-password", async (UserManager<IdentityUser> userManager, ForgotPasswordRequest request) =>
{
    var user = await userManager.FindByEmailAsync(request.Email);

    if (user != null)
    {
       var token = await userManager.GeneratePasswordResetTokenAsync(user); 
       Console.WriteLine($"[FAKE EMAIL] Token para {request.Email}: {token}");
    }

    Results.Ok(new { Message = "Se o e-mail existir, receberá um link de recuperação"});
});


app.Run();