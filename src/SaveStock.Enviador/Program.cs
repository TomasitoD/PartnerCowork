using DotNetEnv;
using SaveStock.Core.Comun;

// Si hay un .env en la carpeta actual, carga sus variables. Las variables reales del entorno tienen prioridad (RD-10).
if (File.Exists(".env"))
{
    Env.NoClobber().Load(".env");
}

var configuracion = ConfiguracionSaveStock.DesdeVariablesDeEntorno();

Console.WriteLine($"Base de datos: {configuracion.RutaBaseDatos}");
Console.WriteLine("Enviador pendiente de implementar (#14)");
