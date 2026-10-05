using SaveStock.Web.Paginas;

namespace SaveStock.Web.Pages;

/// <summary>Página de inicio: enlaces y, si hay sesión, el nombre y el rol del usuario.</summary>
public class IndexModel : PaginaSaveStock
{
    public async Task OnGetAsync()
    {
        // Página pública: no exige sesión, solo la lee para saludar.
        await CargarSesionAsync();
    }
}
