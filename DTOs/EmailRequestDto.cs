using System.ComponentModel.DataAnnotations;

namespace ValoresReceitas.Api.DTOs;

/// <summary>Email da conta para reenvio de confirmacao.</summary>
public sealed record EmailRequestDto
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }
}