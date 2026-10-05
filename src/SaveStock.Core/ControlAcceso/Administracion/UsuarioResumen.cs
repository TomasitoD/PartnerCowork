using SaveStock.Core.ControlAcceso.Entidades;

namespace SaveStock.Core.ControlAcceso.Administracion;

/// <summary>
/// Lo que ve el administrador de cada usuario en el listado (RF-CA-21). Es un DTO aparte de la
/// entidad a propósito: así nunca viajan el hash de la contraseña ni los tokens.
/// </summary>
/// <param name="CuentaActivada">true si el usuario ya abrió el enlace de activación del correo.</param>
public record UsuarioResumen(int Id, string Nombre, string Correo, Rol Rol, bool Activo, bool CuentaActivada);
