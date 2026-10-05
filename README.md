# SaveStock

## Requisitos

- .NET SDK 10

## Cómo clonar

```bash
git clone https://github.com/TomasitoD/SaveStock.git
cd SaveStock
```

## Cómo instalar dependencias

```bash
dotnet restore
```

## Configuración

La configuración se lee de variables de entorno. Para desarrollo, copia `.env.example` como `.env`
en la raíz y completa los valores (el `.env` no se sube al repositorio).

## Cómo ejecutar

Desde la raíz del repositorio:

```bash
dotnet run --project src/SaveStock.Web       # web en http://localhost:5080 (API en /scalar)
dotnet run --project src/SaveStock.Enviador  # envía los correos pendientes de la cola
```

La base SQLite se crea sola en `datos/` y se conserva entre ejecuciones.

## Cómo probar

```bash
dotnet test
```
