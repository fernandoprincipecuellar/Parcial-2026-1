using Microsoft.AspNetCore.Identity;
using Parcial_2026_1.Models;

namespace Parcial_2026_1.Data;

public static class DbInitializer
{
    public static async Task Initialize(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Asegurar que la base de datos esté creada
        context.Database.EnsureCreated();

        // 1. Crear Rol "Analista"
        string roleName = "Analista";
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }

        // 2. Crear Usuario Analista
        string analistaEmail = "analista@test.com";
        var analistaUser = await userManager.FindByEmailAsync(analistaEmail);
        if (analistaUser == null)
        {
            analistaUser = new IdentityUser
            {
                UserName = analistaEmail,
                Email = analistaEmail,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(analistaUser, "Password123!");
            await userManager.AddToRoleAsync(analistaUser, roleName);
        }

        // 3. Crear Clientes
        if (!context.Clientes.Any())
        {
            var cliente1User = new IdentityUser { UserName = "cliente1@test.com", Email = "cliente1@test.com", EmailConfirmed = true };
            var cliente2User = new IdentityUser { UserName = "cliente2@test.com", Email = "cliente2@test.com", EmailConfirmed = true };

            await userManager.CreateAsync(cliente1User, "Password123!");
            await userManager.CreateAsync(cliente2User, "Password123!");

            var cliente1 = new Cliente
            {
                UsuarioId = cliente1User.Id,
                IngresosMensuales = 5000,
                Activo = true
            };

            var cliente2 = new Cliente
            {
                UsuarioId = cliente2User.Id,
                IngresosMensuales = 3000,
                Activo = true
            };

            context.Clientes.AddRange(cliente1, cliente2);
            await context.SaveChangesAsync();

            // 4. Crear Solicitudes
            var solicitud1 = new SolicitudCredito
            {
                ClienteId = cliente1.Id,
                MontoSolicitado = 10000,
                FechaSolicitud = DateTime.Now.AddDays(-5),
                Estado = EstadoSolicitud.Aprobado
            };

            var solicitud2 = new SolicitudCredito
            {
                ClienteId = cliente2.Id,
                MontoSolicitado = 5000,
                FechaSolicitud = DateTime.Now,
                Estado = EstadoSolicitud.Pendiente
            };

            context.SolicitudesCredito.AddRange(solicitud1, solicitud2);
            await context.SaveChangesAsync();
        }
    }
}
