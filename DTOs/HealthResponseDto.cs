namespace ValoresReceitas.Api.DTOs;

/// <summary>Disponibilidade da API e de sua conexao com o banco.</summary>
public sealed record HealthResponseDto(string Status, string Database);