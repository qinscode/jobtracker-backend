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
        Console.WriteLine("Received Google login request");
        Console.WriteLine($"Token: {model.access_token}");

        if (!ModelState.IsValid)
        {
            Console.WriteLine("Model state is invalid");
            return BadRequest(ModelState);
        }

        try
        {
            Console.WriteLine("Attempting to get user info using access token");


            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", model.access_token);
            var response = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo");

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to get user info: {response.StatusCode}");
                return Unauthorized(new { message = "Invalid access token" });
            }

            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Raw Google API response: {content}");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var googleUser = JsonSerializer.Deserialize<GoogleUserInfo>(content, options);

            Console.WriteLine("User info retrieved successfully");
            Console.WriteLine($"Email: {googleUser?.Email}");
            Console.WriteLine($"Name: {googleUser?.Name}");

            if (googleUser == null || string.IsNullOrEmpty(googleUser.Email))
            {
                Console.WriteLine("Email is null or empty");
                return BadRequest(new { message = "Email is required and was not provided by Google" });
            }

            var user = await _userRepository.GetUserByEmailAsync(googleUser.Email);

            if (user == null)
            {
                Console.WriteLine("User not found, creating new user");
                user = new User
                {
                    Email = googleUser.Email,
                    Username = !string.IsNullOrEmpty(googleUser.Name) ? googleUser.Name : googleUser.Email,
                    Password = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()), 
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _userRepository.CreateUserAsync(user);
                Console.WriteLine($"New user created with ID: {user.Id}");
            }
            else
            {
                Console.WriteLine($"Existing user found with ID: {user.Id}");
            }

            var token = GenerateJwtToken(user);
            Console.WriteLine("JWT token generated");

            return Ok(new { Token = token });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error during Google login: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return StatusCode(500, new { message = "An unexpected error occurred" });
        }
    }

    private string GenerateJwtToken(User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Email),
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

        Console.WriteLine("Login success");
        Console.WriteLine("Token: " + new JwtSecurityTokenHandler().WriteToken(token));


        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LoginModel
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; }
}

public class RegisterModel
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; }

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters long")]
    public string Password { get; set; }
}

public class GoogleLoginModel
{
    [Required(ErrorMessage = "Google token is required")]
    public string access_token { get; set; }
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