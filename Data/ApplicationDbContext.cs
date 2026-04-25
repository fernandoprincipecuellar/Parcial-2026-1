using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Parcial_2026_1.Models;

namespace Parcial_2026_1.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<SolicitudCredito> SolicitudesCredito { get; set; }
}
