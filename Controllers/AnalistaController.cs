using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Parcial_2026_1.Data;
using Parcial_2026_1.Models;

namespace Parcial_2026_1.Controllers;

[Authorize(Roles = "Analista")]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;

    public AnalistaController(ApplicationDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    private string GetCacheKey(string userId) => $"solicitudes_{userId}";

    // GET: Analista
    public async Task<IActionResult> Index()
    {
        var solicitudesPendientes = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderBy(s => s.FechaSolicitud)
            .ToListAsync();

        return View(solicitudesPendientes);
    }

    // POST: Analista/Aprobar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null) return NotFound();
        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["ErrorMessage"] = "Solo se pueden aprobar solicitudes en estado Pendiente.";
            return RedirectToAction(nameof(Index));
        }

        // Validación: MontoSolicitado <= IngresosMensuales * 5
        if (solicitud.MontoSolicitado > solicitud.Cliente?.IngresosMensuales * 5)
        {
            solicitud.Estado = EstadoSolicitud.Rechazado;
            solicitud.MotivoRechazo = "Automático: El monto solicitado supera 5 veces los ingresos mensuales permitidos para aprobación directa.";
            await _context.SaveChangesAsync();
            await InvalidateClientCache(solicitud.Cliente?.UsuarioId);
            
            TempData["ErrorMessage"] = $"La solicitud #{id} fue rechazada automáticamente por exceder el límite de ingresos (5x).";
            return RedirectToAction(nameof(Index));
        }

        solicitud.Estado = EstadoSolicitud.Aprobado;
        await _context.SaveChangesAsync();
        await InvalidateClientCache(solicitud.Cliente?.UsuarioId);

        TempData["SuccessMessage"] = $"Solicitud #{id} aprobada exitosamente.";
        return RedirectToAction(nameof(Index));
    }

    // POST: Analista/Rechazar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(int id, string motivoRechazo)
    {
        if (string.IsNullOrWhiteSpace(motivoRechazo))
        {
            TempData["ErrorMessage"] = "Debe proporcionar un motivo para el rechazo.";
            return RedirectToAction(nameof(Index));
        }

        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null) return NotFound();
        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["ErrorMessage"] = "Solo se pueden rechazar solicitudes en estado Pendiente.";
            return RedirectToAction(nameof(Index));
        }

        solicitud.Estado = EstadoSolicitud.Rechazado;
        solicitud.MotivoRechazo = motivoRechazo;
        await _context.SaveChangesAsync();
        await InvalidateClientCache(solicitud.Cliente?.UsuarioId);

        TempData["SuccessMessage"] = $"Solicitud #{id} rechazada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    private async Task InvalidateClientCache(string? userId)
    {
        if (!string.IsNullOrEmpty(userId))
        {
            await _cache.RemoveAsync(GetCacheKey(userId));
        }
    }
}
