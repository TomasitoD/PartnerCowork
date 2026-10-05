using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.Seguridad;

namespace SaveStock.Tests.Integracion;

/// <summary>
/// Páginas web (#20). Los formularios se envían como lo haría el navegador: primero se abre la
/// página para obtener el token antiforgery (campo oculto + cookie) y después se hace el POST.
/// </summary>
public partial class PaginasTests : IClassFixture<SaveStockFactory>
{
    private const string Contrasena = "clave1234";
    private const string MensajeSinPermiso = "No tienes permiso para realizar esta operación.";
    private const string NombreCookie = "savestock_sesion";

    private readonly SaveStockFactory _fabrica;

    public PaginasTests(SaveStockFactory fabrica)
    {
        _fabrica = fabrica;
    }

    private static string Correo(string prefijo) => $"{prefijo}.{Guid.NewGuid():N}@negocio.com";

    /// <summary>Cliente que guarda cookies como un navegador y no sigue redirecciones (para poder revisarlas).</summary>
    private HttpClient NuevoNavegador() =>
        _fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex CampoAntiforgery();

    /// <summary>El HTML que se ve en pantalla: Razor escribe los acentos como entidades (&amp;#xF3;).</summary>
    private static async Task<string> LeerHtmlAsync(HttpResponseMessage respuesta) =>
        WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());

    /// <summary>Abre la página, toma su token antiforgery y envía el formulario.</summary>
    private static async Task<HttpResponseMessage> EnviarFormularioAsync(
        HttpClient navegador, string paginaConFormulario, string destino, Dictionary<string, string> campos)
    {
        var pagina = await navegador.GetAsync(paginaConFormulario);
        var html = await pagina.Content.ReadAsStringAsync();
        var token = CampoAntiforgery().Match(html);
        Assert.True(token.Success, $"{paginaConFormulario} no tiene un formulario con token antiforgery.");

        campos["__RequestVerificationToken"] = token.Groups[1].Value;
        return await navegador.PostAsync(destino, new FormUrlEncodedContent(campos));
    }

    private static Task<HttpResponseMessage> IniciarSesionAsync(HttpClient navegador, string correo, string contrasena = Contrasena) =>
        EnviarFormularioAsync(navegador, "/iniciar-sesion", "/iniciar-sesion", new()
        {
            ["Correo"] = correo,
            ["Contrasena"] = contrasena,
        });

    private async Task<(Usuario Usuario, HttpClient Navegador)> EntrarComoAsync(string prefijo, Rol rol)
    {
        var usuario = await _fabrica.CrearUsuarioAsync("Usuario " + prefijo, Correo(prefijo), Contrasena, rol);
        var navegador = NuevoNavegador();
        var respuesta = await IniciarSesionAsync(navegador, usuario.Correo);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        return (usuario, navegador);
    }

    private static string TokenDeLaCookie(HttpResponseMessage respuesta)
    {
        var cookie = respuesta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(NombreCookie + "="));
        return cookie.Split(';')[0][(NombreCookie.Length + 1)..];
    }

    private Task<Usuario> LeerUsuarioAsync(int id) =>
        _fabrica.ConsultarBaseAsync(db => db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == id));

    [Fact]
    public async Task El_registro_desde_la_pagina_crea_el_usuario_y_muestra_el_mensaje_del_servicio()
    {
        var correo = Correo("registro");
        using var navegador = NuevoNavegador();

        var respuesta = await EnviarFormularioAsync(navegador, "/registro", "/registro", new()
        {
            ["Nombre"] = "Ana Pérez",
            ["Correo"] = correo,
            ["Contrasena"] = Contrasena,
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("Te enviamos un correo para activar tu cuenta.", await LeerHtmlAsync(respuesta));
        var usuario = await _fabrica.ConsultarBaseAsync(db => db.Usuarios.AsNoTracking().SingleAsync(u => u.Correo == correo));
        Assert.Equal("Ana Pérez", usuario.Nombre);
        Assert.False(usuario.Activo);
    }

    [Fact]
    public async Task El_registro_con_correo_repetido_muestra_el_error_del_servicio()
    {
        var existente = await _fabrica.CrearUsuarioAsync("Ana", Correo("repetido"), Contrasena, Rol.Estandar);
        using var navegador = NuevoNavegador();

        var respuesta = await EnviarFormularioAsync(navegador, "/registro", "/registro", new()
        {
            ["Nombre"] = "Otra Ana",
            ["Correo"] = existente.Correo,
            ["Contrasena"] = Contrasena,
        });

        Assert.Contains("Ya existe una cuenta con ese correo.", await LeerHtmlAsync(respuesta));
    }

    [Fact]
    public async Task Iniciar_sesion_guarda_la_cookie_HttpOnly_y_el_perfil_muestra_el_nombre()
    {
        var usuario = await _fabrica.CrearUsuarioAsync("Beatriz Gómez", Correo("perfil"), Contrasena, Rol.Estandar);
        using var navegador = NuevoNavegador();

        var respuesta = await IniciarSesionAsync(navegador, usuario.Correo);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/perfil", respuesta.Headers.Location?.OriginalString);
        var cookie = respuesta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(NombreCookie + "="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);

        var perfil = await navegador.GetAsync("/perfil");
        Assert.Equal(HttpStatusCode.OK, perfil.StatusCode);
        var html = await LeerHtmlAsync(perfil);
        Assert.Contains("Beatriz Gómez", html);
        Assert.Contains(usuario.Correo, html);
        Assert.Contains("Estándar", html);
    }

    [Fact]
    public async Task Iniciar_sesion_con_contrasena_incorrecta_muestra_el_mensaje_y_no_guarda_cookie()
    {
        var usuario = await _fabrica.CrearUsuarioAsync("Carlos", Correo("incorrecta"), Contrasena, Rol.Estandar);
        using var navegador = NuevoNavegador();

        var respuesta = await IniciarSesionAsync(navegador, usuario.Correo, "otraClave99");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("Correo o contraseña incorrectos.", await LeerHtmlAsync(respuesta));
        Assert.False(respuesta.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(c => c.StartsWith(NombreCookie + "=")));
    }

    [Fact]
    public async Task El_perfil_sin_cookie_redirige_a_iniciar_sesion()
    {
        using var navegador = NuevoNavegador();

        var respuesta = await navegador.GetAsync("/perfil");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/iniciar-sesion", respuesta.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task La_pagina_de_inicio_saluda_al_usuario_con_sesion_y_muestra_su_rol()
    {
        var (usuario, navegador) = await EntrarComoAsync("inicio", Rol.Administrador);
        using var _ = navegador;

        var html = await LeerHtmlAsync(await navegador.GetAsync("/"));

        Assert.Contains($"Hola, <strong>{usuario.Nombre}</strong>", html);
        Assert.Contains("Administrador", html);
    }

    [Fact]
    public async Task Un_Estandar_que_abre_la_administracion_recibe_403()
    {
        var (_, navegador) = await EntrarComoAsync("estandar", Rol.Estandar);
        using var _navegador = navegador;

        var respuesta = await navegador.GetAsync("/admin/usuarios");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        var html = await LeerHtmlAsync(respuesta);
        Assert.Contains(MensajeSinPermiso, html);
        Assert.DoesNotContain("<table", html);
    }

    [Theory]
    [InlineData("CambiarRol")]
    [InlineData("Desactivar")]
    [InlineData("Reactivar")]
    [InlineData("ForzarRestablecimiento")]
    public async Task Un_POST_armado_a_mano_por_un_Estandar_recibe_403_y_nada_cambia(string accion)
    {
        var (estandar, navegador) = await EntrarComoAsync("estandar", Rol.Estandar);
        using var _navegador = navegador;
        var objetivo = await _fabrica.CrearUsuarioAsync("Objetivo", Correo("objetivo"), Contrasena, Rol.Estandar);
        var hashAntes = objetivo.HashContrasena;

        // El Estándar no ve el formulario de administración: toma un token antiforgery válido de su
        // perfil y arma el POST al handler de administración a mano.
        var respuesta = await EnviarFormularioAsync(navegador, "/perfil", $"/admin/usuarios?handler={accion}", new()
        {
            ["id"] = objetivo.Id.ToString(),
            ["rol"] = "Administrador",
        });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Contains(MensajeSinPermiso, await LeerHtmlAsync(respuesta));

        var despues = await LeerUsuarioAsync(objetivo.Id);
        Assert.Equal(Rol.Estandar, despues.Rol);
        Assert.True(despues.Activo);
        Assert.Equal(hashAntes, despues.HashContrasena);
        Assert.Equal(Rol.Estandar, (await LeerUsuarioAsync(estandar.Id)).Rol);
        Assert.Empty(await _fabrica.ConsultarBaseAsync(db =>
            db.CodigosRecuperacion.Where(c => c.UsuarioId == objetivo.Id).ToListAsync()));
    }

    [Fact]
    public async Task Un_POST_de_administracion_sin_sesion_redirige_a_iniciar_sesion_y_nada_cambia()
    {
        var objetivo = await _fabrica.CrearUsuarioAsync("Objetivo", Correo("objetivo"), Contrasena, Rol.Estandar);
        using var navegador = NuevoNavegador();

        var respuesta = await EnviarFormularioAsync(navegador, "/iniciar-sesion", "/admin/usuarios?handler=Desactivar", new()
        {
            ["id"] = objetivo.Id.ToString(),
        });

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/iniciar-sesion", respuesta.Headers.Location?.OriginalString);
        Assert.True((await LeerUsuarioAsync(objetivo.Id)).Activo);
    }

    [Fact]
    public async Task El_administrador_ve_la_tabla_sin_hashes()
    {
        var (admin, navegador) = await EntrarComoAsync("admin", Rol.Administrador);
        using var _navegador = navegador;
        var otro = await _fabrica.CrearUsuarioAsync("Daniela Ruiz", Correo("otro"), Contrasena, Rol.Estandar, activado: false);

        var respuesta = await navegador.GetAsync("/admin/usuarios");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = await LeerHtmlAsync(respuesta);
        Assert.Contains("<table", html);
        Assert.Contains(admin.Correo, html);
        Assert.Contains("Daniela Ruiz", html);
        Assert.Contains(otro.Correo, html);
        Assert.DoesNotContain("pbkdf2-sha256", html);
        Assert.DoesNotContain(otro.HashContrasena, html);
        Assert.DoesNotContain(admin.HashContrasena, html);
    }

    [Fact]
    public async Task El_administrador_desactiva_y_cambia_el_rol_desde_la_tabla()
    {
        var (_, navegador) = await EntrarComoAsync("admin", Rol.Administrador);
        using var _navegador = navegador;
        var otro = await _fabrica.CrearUsuarioAsync("Esteban", Correo("otro"), Contrasena, Rol.Estandar);

        var desactivar = await EnviarFormularioAsync(navegador, "/admin/usuarios", "/admin/usuarios?handler=Desactivar", new()
        {
            ["id"] = otro.Id.ToString(),
        });
        Assert.Contains("El usuario fue desactivado y sus sesiones se cerraron.", await LeerHtmlAsync(desactivar));

        var cambiarRol = await EnviarFormularioAsync(navegador, "/admin/usuarios", "/admin/usuarios?handler=CambiarRol", new()
        {
            ["id"] = otro.Id.ToString(),
            ["rol"] = "Administrador",
        });
        Assert.Contains("El rol del usuario se actualizó.", await LeerHtmlAsync(cambiarRol));

        var despues = await LeerUsuarioAsync(otro.Id);
        Assert.False(despues.Activo);
        Assert.Equal(Rol.Administrador, despues.Rol);
    }

    [Fact]
    public async Task Cerrar_sesion_revoca_la_sesion_de_la_cookie()
    {
        var usuario = await _fabrica.CrearUsuarioAsync("Fernanda", Correo("cerrar"), Contrasena, Rol.Estandar);
        using var navegador = NuevoNavegador();
        var inicio = await IniciarSesionAsync(navegador, usuario.Correo);
        var token = TokenDeLaCookie(inicio);

        var cerrar = await EnviarFormularioAsync(navegador, "/perfil", "/perfil?handler=CerrarSesion", []);

        Assert.Equal(HttpStatusCode.Redirect, cerrar.StatusCode);
        Assert.Equal("/", cerrar.Headers.Location?.OriginalString);

        // La sesión quedó revocada en la base: el mismo token ya no sirve ni en las páginas ni en la API.
        var hash = GeneradorTokens.CalcularHash(token);
        var sesion = await _fabrica.ConsultarBaseAsync(db => db.Sesiones.AsNoTracking().SingleAsync(s => s.HashToken == hash));
        Assert.NotNull(sesion.FechaRevocacion);

        using var conTokenViejo = _fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var perfil = new HttpRequestMessage(HttpMethod.Get, "/perfil");
        perfil.Headers.Add("Cookie", $"{NombreCookie}={token}");
        Assert.Equal(HttpStatusCode.Redirect, (await conTokenViejo.SendAsync(perfil)).StatusCode);

        conTokenViejo.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await conTokenViejo.GetAsync("/api/sesion/yo")).StatusCode);
    }

    [Fact]
    public async Task Cambiar_la_contrasena_desde_el_perfil_cierra_la_sesion()
    {
        var usuario = await _fabrica.CrearUsuarioAsync("Gabriel", Correo("cambio"), Contrasena, Rol.Estandar);
        using var navegador = NuevoNavegador();
        await IniciarSesionAsync(navegador, usuario.Correo);

        var respuesta = await EnviarFormularioAsync(navegador, "/perfil", "/perfil?handler=CambiarContrasena", new()
        {
            ["ContrasenaActual"] = Contrasena,
            ["ContrasenaNueva"] = "nuevaClave55",
        });

        Assert.Contains("Contraseña actualizada. Inicia sesión de nuevo.", await LeerHtmlAsync(respuesta));
        Assert.Equal(HttpStatusCode.Redirect, (await navegador.GetAsync("/perfil")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await IniciarSesionAsync(navegador, usuario.Correo, "nuevaClave55")).StatusCode);
    }

    [Fact]
    public async Task Recuperar_muestra_el_mismo_mensaje_exista_o_no_el_correo()
    {
        var usuario = await _fabrica.CrearUsuarioAsync("Hilda", Correo("recuperar"), Contrasena, Rol.Estandar);
        const string mensaje = "Si el correo está registrado, te enviamos un código para restablecer tu contraseña.";

        foreach (var correo in new[] { usuario.Correo, Correo("noexiste") })
        {
            using var navegador = NuevoNavegador();
            var respuesta = await EnviarFormularioAsync(navegador, "/contrasena/recuperar", "/contrasena/recuperar", new()
            {
                ["Correo"] = correo,
            });
            Assert.Contains(mensaje, await LeerHtmlAsync(respuesta));
        }
    }

    [Fact]
    public async Task Restablecer_con_un_codigo_invalido_muestra_el_error_y_la_contrasena_no_cambia()
    {
        var usuario = await _fabrica.CrearUsuarioAsync("Iván", Correo("restablecer"), Contrasena, Rol.Estandar);
        using var navegador = NuevoNavegador();

        var respuesta = await EnviarFormularioAsync(navegador, "/contrasena/restablecer", "/contrasena/restablecer", new()
        {
            ["Correo"] = usuario.Correo,
            ["Codigo"] = "ABCDEFGH",
            ["ContrasenaNueva"] = "nuevaClave55",
        });

        Assert.Contains("El código no es válido o ya venció.", await LeerHtmlAsync(respuesta));
        Assert.Equal(usuario.HashContrasena, (await LeerUsuarioAsync(usuario.Id)).HashContrasena);
    }

    [Fact]
    public async Task Un_POST_sin_token_antiforgery_se_rechaza()
    {
        var (_, navegador) = await EntrarComoAsync("admin", Rol.Administrador);
        using var _navegador = navegador;
        var otro = await _fabrica.CrearUsuarioAsync("Julia", Correo("otro"), Contrasena, Rol.Estandar);

        var respuesta = await navegador.PostAsync("/admin/usuarios?handler=Desactivar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = otro.Id.ToString(),
        }));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await LeerUsuarioAsync(otro.Id)).Activo);
    }
}
