using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parcial_2026_1.Data;
using Parcial_2026_1.Models;

namespace Parcial_2026_1.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public SolicitudesController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: Solicitudes
    public async Task<IActionResult> Index(
        EstadoSolicitud? estado, 
        decimal? montoMin, 
        decimal? montoMax, 
        DateOnly? fechaInicio, 
        DateOnly? fechaFin)
    {
        var userId = _userManager.GetUserId(User);
        var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);

        if (cliente == null)
        {
            return NotFound("No se encontró el perfil de cliente para este usuario.");
        }

        // Validaciones
        if (montoMin < 0) ModelState.AddModelError("montoMin", "El monto mínimo no puede ser negativo.");
        if (montoMax < 0) ModelState.AddModelError("montoMax", "El monto máximo no puede ser negativo.");
        if (fechaInicio.HasValue && fechaFin.HasValue && fechaInicio > fechaFin)
        {
            ModelState.AddModelError("fechaInicio", "La fecha de inicio no puede ser posterior a la fecha de fin.");
        }

        var query = _context.SolicitudesCredito
            .Where(s => s.ClienteId == cliente.Id)
            .AsQueryable();

        // Aplicar filtros solo si ModelState es válido
        if (ModelState.IsValid)
        {
            if (estado.HasValue)
            {
                query = query.Where(s => s.Estado == estado.Value);
            }

            if (montoMin.HasValue)
            {
                query = query.Where(s => s.MontoSolicitado >= montoMin.Value);
            }

            if (montoMax.HasValue)
            {
                query = query.Where(s => s.MontoSolicitado <= montoMax.Value);
            }

            if (fechaInicio.HasValue)
            {
                var dtInicio = fechaInicio.Value.ToDateTime(TimeOnly.MinValue);
                query = query.Where(s => s.FechaSolicitud >= dtInicio);
            }

            if (fechaFin.HasValue)
            {
                var dtFin = fechaFin.Value.ToDateTime(TimeOnly.MaxValue);
                query = query.Where(s => s.FechaSolicitud <= dtFin);
            }
        }

        ViewBag.Estado = estado;
        ViewBag.MontoMin = montoMin;
        ViewBag.MontoMax = montoMax;
        ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
        ViewBag.FechaFin = fechaFin?.ToString("yyyy-MM-dd");

        var solicitudes = await query.OrderByDescending(s => s.FechaSolicitud).ToListAsync();
        return View(solicitudes);
    }

    // GET: Solicitudes/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var userId = _userManager.GetUserId(User);
        var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);

        if (cliente == null) return NotFound();

        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(m => m.Id == id && m.ClienteId == cliente.Id);

        if (solicitud == null) return NotFound();

        return View(solicitud);
    }
}
