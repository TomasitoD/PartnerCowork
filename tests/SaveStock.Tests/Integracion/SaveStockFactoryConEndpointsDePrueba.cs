using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Web.Filtros;

namespace SaveStock.Tests.Integracion;

/// <summary>
/// Igual que <see cref="SaveStockFactory"/>, pero agrega endpoints que solo existen en las pruebas
/// (fuera de /api) para probar el manejo de errores y el filtro de permisos sin depender de las features.
/// </summary>
public class SaveStockFactoryConEndpointsDePrueba : SaveStockFactory
{
    public record DatosPrueba(string Nombre, int Cantidad);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(servicios => servicios.AddSingleton<IStartupFilter, EndpointsDePrueba>());
    }

    private sealed class EndpointsDePrueba : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> siguiente)
        {
            return app =>
            {
                // Primero el pipeline de Program (con el manejador de errores); estos endpoints quedan detrás.
                siguiente(app);
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapPost("/prueba/json", (DatosPrueba datos) => Results.Ok(datos))
                        .RequiereOperacion(Operacion.Salud);

                    endpoints.MapGet("/prueba/error", IResult () => throw new InvalidOperationException("Detalle interno: SELECT * FROM Usuarios"))
                        .RequiereOperacion(Operacion.Salud);

                    endpoints.MapGet("/prueba/autenticado", (HttpContext http) => Results.Ok(new { correo = http.ObtenerUsuarioActual().Correo }))
                        .RequiereOperacion(Operacion.ConsultarUsuarioActual);

                    endpoints.MapGet("/prueba/administrador", () => Results.Ok())
                        .RequiereOperacion(Operacion.ListarUsuarios);
                });
            };
        }
    }
}
