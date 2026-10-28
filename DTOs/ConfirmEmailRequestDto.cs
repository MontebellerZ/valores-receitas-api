using System.ComponentModel.DataAnnotations;

namespace ValoresReceitas.Api.DTOs;

/// <summary>Identificador e token recebidos no link de confirmacao.</summary>
public sealed record ConfirmEmailRequestDto
{
    [Required]
    public required string UserId { get; init; }

    [Required]
    public required string Token { get; init; }
}