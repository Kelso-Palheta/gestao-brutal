using System;
using System.Collections.Generic;

namespace BatatasFritas.Shared.DTOs;

/// <summary>
/// Resultado paginado genérico. Retornado por endpoints que suportam paginação server-side.
/// Contrato: GET /api/recurso?page=1&pageSize=20
/// </summary>
public class PagedResult<T>
{
    /// <summary>Registros da página atual.</summary>
    public List<T> Items { get; set; } = new();

    /// <summary>Total de registros na tabela/filtro (sem paginação).</summary>
    public int TotalCount { get; set; }

    /// <summary>Página atual (base 1).</summary>
    public int Page { get; set; }

    /// <summary>Tamanho máximo da página (máx 100).</summary>
    public int PageSize { get; set; }

    /// <summary>Total de páginas: ceil(TotalCount / PageSize).</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    public static PagedResult<T> Empty(int page, int pageSize) => new()
    {
        Items     = new List<T>(),
        TotalCount = 0,
        Page      = page,
        PageSize  = pageSize
    };
}
