namespace SaveStock.Core.ControlAcceso.Entidades;

public class Usuario
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Único. Se guarda en minúsculas y sin espacios (ver ValidadorEntrada.NormalizarCorreo).</summary>
    public string Correo { get; set; } = string.Empty;

    /// <summary>Hash PBKDF2 con sal; nunca la contraseña en texto plano (RF-CA-02, RD-05).</summary>
    public string HashContrasena { get; set; } = string.Empty;

    public Rol Rol { get; set; } = Rol.Estandar;

    /// <summary>false = desactivado por un administrador (RF-CA-20) o todavía no activado.</summary>
    public bool Activo { get; set; }

    /// <summary>null = todavía no abrió el enlace de activación (RF-CA-15).</summary>
    public DateTime? FechaActivacion { get; set; }

    /// <summary>Intentos fallidos consecutivos de inicio de sesión (RF-CA-19).</summary>
    public int IntentosFallidos { get; set; }

    /// <summary>Mientras sea mayor que la hora actual, la cuenta no puede iniciar sesión (RF-CA-19).</summary>
    public DateTime? BloqueadoHasta { get; set; }

    public DateTime FechaCreacion { get; set; }
}
