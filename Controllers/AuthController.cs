using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using JobTracker.Models;
using JobTracker.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace JobTracker.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IUserRepository _userRepository;

    public AuthController(IUserRepository userRepository, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _userRepository.GetUserByEmailAsync(model.Email);

        if (user == null || !user.VerifyPassword(model.Password))
            return Unauthorized(new { message = "Invalid email or password" });

        var token = GenerateJwtToken(user);

        return Ok(new { Token = token });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var existingUser = await _userRepository.GetUserByEmailAsync(model.Email);
        if (existingUser != null)
            return BadRequest(new { message = "User with this email already exists" });

        var newUser = new User
        {
            Email = model.Email,
            Username = model.Username
        };
        newUser.SetPassword(model.Password);

        await _userRepository.CreateUserAsync(newUser);

        var token = GenerateJwtToken(newUser);

        return Ok(new { Token = token });
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", model.access_token);
            var response = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo");

            if (!response.IsSuccessStatusCode)
            {
                return Unauthorized(new { message = "Invalid access token" });
            }

            var content = await response.Content.ReadAsStringAsync();


            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var googleUser = JsonSerializer.Deserialize<GoogleUserInfo>(content, options);


            if (googleUser == null || string.IsNullOrEmpty(googleUser.Email))
            {
                return BadRequest(new { message = "Email is required and was not provided by Google" });
            }

            var user = await _userRepository.GetUserByEmailAsync(googleUser.Email);

            if (user == null)
            {
                user = new User
                {
                    Email = googleUser.Email,
                    Username = !string.IsNullOrEmpty(googleUser.Name) ? googleUser.Name : googleUser.Email,
                    Password = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _userRepository.CreateUserAsync(user);
            }


            var token = GenerateJwtToken(user);

            return Ok(new { Token = token });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An unexpected error occurred" });
        }
    }

    private string GenerateJwtToken(User user)
    {
        var jwtKey = _configuration["Jwt:Key"] ??
                     throw new InvalidOperationException("JWT key is not configured");
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        var token = new JwtSecurityToken(
            _configuration["Jwt:Issuer"],
            _configuration["Jwt:Audience"],
            claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LoginModel
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}

public class RegisterModel
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters long")]
    public string Password { get; set; } = string.Empty;
}

public class GoogleLoginModel
{
    [Required(ErrorMessage = "Google token is required")]
    public string access_token { get; set; } = string.Empty;
}

public class GoogleUserInfo
{
    public string? Sub { get; set; }
    public string? Name { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? Picture { get; set; }
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
}