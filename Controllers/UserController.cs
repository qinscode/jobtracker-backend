using System.IdentityModel.Tokens.Jwt;
using JobTracker.Models;
using JobTracker.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;

    public UserController(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    [HttpGet]
    public async Task<ActionResult<UserDto>> GetUser()
    {
        var userGuid = GetUserIdFromToken();
        var user = await _userRepository.GetUserByIdAsync(userGuid);
        if (user == null) return NotFound();
        return Ok(new UserDto { Username = user.Username, Email = user.Email });
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser(User user)
    {
        if (string.IsNullOrEmpty(user.Password)) return BadRequest("Password is required");

        // Check if the email already exists
        var existingUser = await _userRepository.GetUserByEmailAsync(user.Email);
        if (existingUser != null) return BadRequest("Email is already in use");

        var createdUser = await _userRepository.CreateUserAsync(user);

        var userDto = new UserDto { Username = createdUser.Username, Email = createdUser.Email };
        return CreatedAtAction(nameof(GetUser), new { id = createdUser.Id }, userDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, User user)
    {
        // check if the id in the URL matches the id in the request body
        if (id != user.Id) return BadRequest();

        try
        {
            await _userRepository.UpdateUserAsync(id, user);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        await _userRepository.DeleteUserAsync(id);
        return NoContent();
    }

    private Guid GetUserIdFromToken()
    {
        var token = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

        var userId = jsonToken?.Claims.FirstOrDefault(claim =>
            claim.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            throw new UnauthorizedAccessException("Invalid or missing user ID in the token");

        return userGuid;
    }
}