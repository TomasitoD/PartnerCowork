namespace SaveStock.Core.ControlAcceso.Contrasenas;

/// <summary>Cuerpo de POST /api/contrasena/recuperar (RF-CA-09).</summary>
public record SolicitudRecuperacion(string? Correo);

/// <summary>Cuerpo de POST /api/contrasena/restablecer (RF-CA-10, RF-CA-11).</summary>
public record SolicitudRestablecimiento(string? Correo, string? Codigo, string? ContrasenaNueva);
