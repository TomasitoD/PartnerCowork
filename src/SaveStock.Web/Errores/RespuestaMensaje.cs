namespace SaveStock.Web.Errores;

/// <summary>
/// Cuerpo JSON <c>{ "mensaje": "..." }</c>. Lo usan todas las respuestas de error (RD-08)
/// y las respuestas exitosas que solo informan algo.
/// </summary>
public record RespuestaMensaje(string Mensaje);
