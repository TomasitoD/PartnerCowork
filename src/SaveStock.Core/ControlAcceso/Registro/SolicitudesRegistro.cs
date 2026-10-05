namespace SaveStock.Core.ControlAcceso.Registro;

/// <summary>
/// Datos del formulario de registro. Los campos son opcionales a propósito: si falta alguno,
/// el servicio responde con un mensaje de validación en lugar de una excepción (RD-07).
/// </summary>
public record SolicitudRegistro(string? Nombre, string? Correo, string? Contrasena);

/// <summary>Pedido de un nuevo enlace de activación (RF-CA-17).</summary>
public record SolicitudReenvioActivacion(string? Correo);
