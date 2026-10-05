# SaveStock

SaveStock es un inventario para negocios pequeños. La **Práctica 1** entrega la primera pieza del Core
funcionando completa, el **control de acceso** (registro con activación por correo, sesión con bloqueo
por intentos, roles y administración de usuarios, recuperación y cambio de contraseña, y la cola de
correos con su enviador aparte), y deja declarada la **estructura de la máquina de estados** de la orden
de compra, la entidad central del inventario.

Todos los comandos de este README se ejecutan **desde la raíz del repositorio**.

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) (`dotnet --version` debe mostrar `10.x`).
- `curl`, para las verificaciones de abajo.
- Opcionales: `sqlite3`, para leer la base de datos (viene instalado en macOS y en casi todas las
  distribuciones de Linux), y `jq`, para sacar el token de las respuestas JSON (cada comando que lo usa
  trae también la versión sin `jq`).

Los comandos están escritos para bash o zsh (macOS, Linux, o Git Bash / WSL en Windows).

## Instalación

```bash
git clone https://github.com/TomasitoD/SaveStock.git
cd SaveStock
git checkout practica-1
dotnet restore
```

## Variables de entorno

La configuración se lee **solo** de variables de entorno (RD-10); en el repositorio no hay ninguna
credencial. Hay dos formas de darlas:

- **Archivo `.env` (recomendado):** copia la plantilla y completa los valores. El `.env` está en
  `.gitignore`, así que nunca se sube. La web y el enviador lo cargan solos al arrancar.

  ```bash
  cp .env.example .env
  ```

- **Variables exportadas** en la terminal (`export SMTP_HOST=...`). Si una variable está definida en el
  entorno y también en el `.env`, gana la del entorno. Eso sirve para cambiar una sola variable en un
  comando, por ejemplo `SMTP_HOST=smtp.invalido dotnet run --project src/SaveStock.Enviador`.

| Variable | Para qué sirve | Obligatoria |
|---|---|---|
| `SAVESTOCK_DB_RUTA` | Archivo SQLite del Core (usuarios, sesiones, tokens, cola de correos). La web y el enviador usan el mismo. Si está vacía: `datos/savestock.db`. | No |
| `SAVESTOCK_INVENTARIO_DB_RUTA` | Archivo SQLite del inventario (órdenes de compra). Si está vacía: `datos/inventario.db`. | No |
| `SAVESTOCK_URL_BASE` | URL pública de la web, con la que se arman los enlaces de los correos. Si está vacía: `http://localhost:5080`. | No |
| `SAVESTOCK_ADMIN_CORREO` | Correo del administrador inicial. Se crea activado al iniciar la web si todavía no existe. | Sí, para tener un Administrador |
| `SAVESTOCK_ADMIN_CONTRASENA` | Contraseña del administrador inicial (al menos 8 caracteres, con letras y números). | Sí, para tener un Administrador |
| `SAVESTOCK_ADMIN_NOMBRE` | Nombre del administrador inicial. | Sí, para tener un Administrador |
| `SMTP_HOST` | Servidor SMTP con el que el enviador manda los correos. | Sí, para el enviador |
| `SMTP_PUERTO` | Puerto del servidor SMTP. Si está vacía: `587`. | No |
| `SMTP_USUARIO` | Usuario con el que el enviador se autentica en el servidor SMTP. | Sí, para el enviador |
| `SMTP_CONTRASENA` | Contraseña del usuario SMTP. | Sí, para el enviador |
| `SMTP_REMITENTE` | Dirección que aparece como remitente de los correos. | Sí, para el enviador |

Si falta alguna de las tres `SAVESTOCK_ADMIN_*`, no se crea el administrador inicial. Las variables
`SMTP_*` solo las lee el enviador: la web nunca se conecta al servidor de correo.

**Correo real con Gmail.** Sirve cualquier servidor SMTP. Con una cuenta de Gmail:

1. Activa la verificación en dos pasos de la cuenta de Google.
2. Crea una contraseña de aplicación en <https://myaccount.google.com/apppasswords>.
3. En el `.env`: `SMTP_HOST=smtp.gmail.com`, `SMTP_PUERTO=587`, tu dirección de Gmail en
   `SMTP_USUARIO` y en `SMTP_REMITENTE`, y la contraseña de aplicación (16 letras, sin espacios) en
   `SMTP_CONTRASENA`. Nunca tu contraseña normal.
