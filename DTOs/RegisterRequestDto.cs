using System.ComponentModel.DataAnnotations;

namespace ValoresReceitas.Api.DTOs;

/// <summary>Credenciais necessarias a criacao de uma conta.</summary>
public sealed record RegisterRequestDto
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required, MinLength(6)]
    public required string Password { get; init; }
}