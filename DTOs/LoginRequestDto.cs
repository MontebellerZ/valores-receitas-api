using System.ComponentModel.DataAnnotations;

namespace ValoresReceitas.Api.DTOs;

/// <summary>Credenciais para iniciar uma sessao.</summary>
public sealed record LoginRequestDto
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}