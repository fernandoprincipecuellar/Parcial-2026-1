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

    // GET: Solicitudes/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Solicitudes/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("MontoSolicitado")] SolicitudCredito solicitud)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account", new { area = "Identity" });

        var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);

        // 1. Validar existencia y estado del cliente
        if (cliente == null || !cliente.Activo)
        {
            ModelState.AddModelError(string.Empty, "No tienes un perfil de cliente activo para realizar esta solicitud.");
            return View(solicitud);
        }

        // 2. Validar que no tenga solicitudes pendientes
        bool tienePendiente = await _context.SolicitudesCredito
            .AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);
        
        if (tienePendiente)
        {
            ModelState.AddModelError(string.Empty, "Ya tienes una solicitud de crédito en estado Pendiente.");
            return View(solicitud);
        }

        // 3. Validar monto solicitado vs ingresos (Max 10 veces)
        if (solicitud.MontoSolicitado > cliente.IngresosMensuales * 10)
        {
            ModelState.AddModelError("MontoSolicitado", $"El monto máximo permitido es {cliente.IngresosMensuales * 10:C2} (10 veces tus ingresos mensuales).");
            return View(solicitud);
        }

        if (ModelState.IsValid)
        {
            solicitud.ClienteId = cliente.Id;
            solicitud.Estado = EstadoSolicitud.Pendiente;
            solicitud.FechaSolicitud = DateTime.Now;

            _context.Add(solicitud);
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Solicitud de crédito creada exitosamente. Está siendo evaluada.";
            return RedirectToAction(nameof(Index));
        }

        return View(solicitud);
    }
}
