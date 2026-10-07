namespace CryptoPortfolioTracker.Helpers;

/// <summary>
/// Spring vanuit één pagina naar een andere met een opdracht (v1.49, AI Research).
/// De doelpagina pakt de opdracht op in zijn Loaded-handler met <see cref="Take{T}"/>; zo voert elke pagina
/// de actie uit met zijn eigen, volledig geladen context (lijsten, dialogen) in plaats van een kopie daarvan.
/// </summary>
public static class AppNavigator
{
    private static AppNavigationRequest? _pending;

    /// <summary>Navigeer naar de pagina met deze <c>Tag</c> (zie MainPage.xaml) en laat de opdracht klaarstaan.</summary>
    public static bool Request(string viewTag, AppNavigationRequest request)
    {
        _pending = request;
        if (MainPage.Current?.NavigateTo(viewTag) == true) return true;
        _pending = null;
        return false;
    }

    /// <summary>Haal de klaarstaande opdracht op als die van dit type is (eenmalig).</summary>
    public static T? Take<T>() where T : AppNavigationRequest
    {
        if (_pending is not T t) return null;
        _pending = null;
        return t;
    }
}
