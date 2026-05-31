using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace OrderManagement.WebApi.Models;

public class AuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;

    public AuthService(ApplicationDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    public async Task<User> CreateUserAsync(string email, string firstName, string lastName, string phone, string password, string confirmPassword)
    {
        if (password != confirmPassword) throw new InvalidOperationException("Passwords do not match");
        if (await _context.Users.AnyAsync(u => u.Email == email)) throw new InvalidOperationException("Email already exists");

        var user = new User
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Phone = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<object> LoginAsync(string email, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password");

        var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JwtSettings:SecretKey"] ?? "secret"));
        var creds = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _config["JwtSettings:Issuer"],
            audience: _config["JwtSettings:Audience"],
            claims: new[] {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            },
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);

        return new { Token = new JwtSecurityTokenHandler().WriteToken(token), User = user };
    }
}
