using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using Parcial_2026_1.Data;
using Parcial_2026_1.Models;

namespace Parcial_2026_1.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IDistributedCache _cache;

    public SolicitudesController(ApplicationDbContext context, UserManager<IdentityUser> userManager, IDistributedCache cache)
    {
        _context = context;
        _userManager = userManager;
        _cache = cache;
    }

    private string GetCacheKey(string userId) => $"solicitudes_{userId}";

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

        List<SolicitudCredito>? solicitudes = null;

        // Intentar obtener del caché solo si no hay filtros activos y no hay errores
        bool hasFilters = estado.HasValue || montoMin.HasValue || montoMax.HasValue || fechaInicio.HasValue || fechaFin.HasValue;

        if (!hasFilters && ModelState.IsValid)
        {
            var cacheKey = GetCacheKey(userId!);
            var cachedData = await _cache.GetStringAsync(cacheKey);
            if (cachedData != null)
            {
                solicitudes = JsonSerializer.Deserialize<List<SolicitudCredito>>(cachedData);
            }
        }

        if (solicitudes == null)
        {
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

            solicitudes = await query.OrderByDescending(s => s.FechaSolicitud).ToListAsync();

            // Guardar en caché solo si no hay filtros
            if (!hasFilters && ModelState.IsValid)
            {
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
                };
                var serialized = JsonSerializer.Serialize(solicitudes);
                await _cache.SetStringAsync(GetCacheKey(userId!), serialized, cacheOptions);
            }
        }

        ViewBag.Estado = estado;
        ViewBag.MontoMin = montoMin;
        ViewBag.MontoMax = montoMax;
        ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
        ViewBag.FechaFin = fechaFin?.ToString("yyyy-MM-dd");

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

        // Guardar en sesión la última solicitud visitada
        HttpContext.Session.SetString("ultima_solicitud", JsonSerializer.Serialize(new
        {
            Id = solicitud.Id,
            Monto = solicitud.MontoSolicitado
        }));

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

            // Invalidar caché Redis
            await _cache.RemoveAsync(GetCacheKey(userId));
            
            TempData["SuccessMessage"] = "Solicitud de crédito creada exitosamente. Está siendo evaluada.";
            return RedirectToAction(nameof(Index));
        }

        return View(solicitud);
    }

    // POST: Solicitudes/CambiarEstado (para invalidar caché en cambios de estado)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id, EstadoSolicitud nuevoEstado, string? motivoRechazo)
    {
        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null) return NotFound();

        solicitud.Estado = nuevoEstado;
        if (nuevoEstado == EstadoSolicitud.Rechazado)
        {
            solicitud.MotivoRechazo = motivoRechazo;
        }

        await _context.SaveChangesAsync();

        // Invalidar caché Redis del cliente dueño de la solicitud
        var clienteUserId = solicitud.Cliente?.UsuarioId;
        if (!string.IsNullOrEmpty(clienteUserId))
        {
            await _cache.RemoveAsync(GetCacheKey(clienteUserId));
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
