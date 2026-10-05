using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using SaveStock.Core;
using SaveStock.Core.Comun;
using SaveStock.Core.Correo;
using SaveStock.Core.Datos;
using SaveStock.Enviador;

// SaveStock.Enviador: proceso aparte que manda los correos de la cola (RF-NOT-09).
// Se ejecuta desde la raíz del repo con: dotnet run --project src/SaveStock.Enviador

// Si hay un .env en la carpeta actual, carga sus variables. Las variables reales del entorno tienen prioridad (RD-10).
if (File.Exists(".env"))
{
    Env.NoClobber().Load(".env");
}

var configuracion = ConfiguracionSaveStock.DesdeVariablesDeEntorno();

// Las credenciales SMTP salen solo de variables de entorno (RF-NOT-13). Si falta alguna, se avisa y se termina.
var smtp = ConfiguracionSmtp.Validar(configuracion);
if (!smtp.Exito)
{
    Console.Error.WriteLine(smtp.Mensaje);
    return 1;
}

var servicios = new ServiceCollection();
servicios.AddLogging();
servicios.AddSaveStockCore(configuracion);
servicios.AddSingleton(smtp.Valor!);
servicios.AddProcesadorColaCorreos<EnviadorSmtp>();

try
{
    await using var proveedor = servicios.BuildServiceProvider();

    // Misma base que la web: crea las tablas si todavía no existen (RD-09).
    await InicializadorBaseDatos.InicializarAsync(proveedor);

    Console.WriteLine($"Base de datos: {configuracion.RutaBaseDatos}");
    Console.WriteLine($"Servidor SMTP: {smtp.Valor!.Host}:{smtp.Valor.Puerto}");

    using var alcance = proveedor.CreateScope();
    var procesador = alcance.ServiceProvider.GetRequiredService<ProcesadorColaCorreos>();
    var resumen = await procesador.ProcesarAsync();

    foreach (var fallo in resumen.Fallos)
    {
        Console.WriteLine($"No se pudo enviar el correo {fallo.CorreoId} a {fallo.Destinatario}: {fallo.Error} Queda Pendiente.");
    }

    Console.WriteLine($"Enviados: {resumen.Enviados}, fallidos: {resumen.Fallidos}, pendientes: {resumen.Pendientes}");
    return 0;
}
catch (Exception ex)
{
    // Sin traza ni detalles internos: solo el tipo de error (RD-08).
    Console.Error.WriteLine($"El enviador terminó por un error inesperado ({ex.GetType().Name}). Revisa SAVESTOCK_DB_RUTA e intenta de nuevo.");
    return 1;
}
