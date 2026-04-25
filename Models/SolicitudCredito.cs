using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Parcial_2026_1.Models;

public enum EstadoSolicitud
{
    Pendiente,
    Aprobado,
    Rechazado
}

public class SolicitudCredito
{
    public int Id { get; set; }

    [Required]
    public int ClienteId { get; set; }

    [ForeignKey("ClienteId")]
    public virtual Cliente? Cliente { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal MontoSolicitado { get; set; }

    public DateTime FechaSolicitud { get; set; } = DateTime.Now;

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    public string? MotivoRechazo { get; set; }
}
