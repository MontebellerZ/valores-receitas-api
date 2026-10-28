namespace ValoresReceitas.Api.DTOs;

/// <summary>Identificacao publica da sessao, sem dados de seguranca do Identity.</summary>
public sealed record SessionResponseDto(string Id, string Email);