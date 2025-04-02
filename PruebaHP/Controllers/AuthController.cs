﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using PruebaHP.Model;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.EntityFrameworkCore;
using PruebaHP.Data;

namespace PruebaHP.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly PersonajeContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(PersonajeContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.NombreUsuario) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { message = "Usuario y contraseña son obligatorios." });
            }

            // 🔹 Obtener la conexión de Entity Framework
            using (var connection = (SqlConnection)_context.Database.GetDbConnection())
            {
                await connection.OpenAsync();

                using (var cmd = new SqlCommand("ValidarUsuario", connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@NombreUsuario", request.NombreUsuario);
                    cmd.Parameters.AddWithValue("@Password", request.Password);

                    int resultado = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                    if (resultado == 1)
                    {
                        var secretKey = _configuration["JwtSettings:SecretKey"];
                        if (string.IsNullOrEmpty(secretKey))
                        {
                            return StatusCode(500, new { message = "Error en la configuración del token." });
                        }

                        var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey));
                        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                        var claims = new[]
                        {
                            new Claim(ClaimTypes.Name, request.NombreUsuario),
                            new Claim(ClaimTypes.Role, "User")
                        };

                        var token = new JwtSecurityToken(
                            claims: claims,
                            expires: DateTime.UtcNow.AddHours(1),
                            signingCredentials: creds
                        );

                        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
                        return Ok(new { token = tokenString });
                    }
                }
            }

            return Unauthorized(new { message = "Usuario o contraseña incorrectos." });
        }
    }

    public class LoginRequest
    {
        public string? NombreUsuario { get; set; }
        public string? Password { get; set; }
    }
}