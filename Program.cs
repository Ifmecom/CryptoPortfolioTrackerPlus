namespace CryptoPortfolioTracker;

/// <summary>
/// Eigen startpunt (i.p.v. de door XAML gegenereerde Main, zie DISABLE_XAML_GENERATED_MAIN in de csproj).
/// Identiek aan de gegenereerde versie, met één toevoeging vooraf — zie <see cref="AvoidMrmHeapOverflow"/>.
/// </summary>
public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        AvoidMrmHeapOverflow();

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start(_ =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }

    /// <summary>
    /// Omzeilt een heap-bufferoverloop in Windows App SDK MRM (<c>MrmGetFilePathFromName</c>), de oorzaak van de
    /// willekeurige opstartcrash 0xC0000374 (heapcorruptie). Vastgesteld met WinDbg + page heap, okt 2026.
    /// <para>
    /// Bij het starten van XAML zoekt MRM <c>resources.pri</c>. Staat <c>MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY</c>
    /// (gezet door de ModuleInitializer van de WinAppSDK, alleen nodig voor PublishSingleFile) dan reserveert MRM
    /// de buffer op basis van die map en geeft het een byte- i.p.v. tekenaantal door aan <c>PathCchCombineEx</c>,
    /// dat daardoor met het <c>\\?\</c>-voorvoegsel voorbij de buffer schrijft. Upstream gemeld als
    /// microsoft/WindowsAppSDK#4873; de meegeleverde MRM.dll in 1.5.x én 1.6.x bevat de fout nog.
    /// </para>
    /// <para>
    /// Zonder de variabele rekent MRM met het volledige exe-pad (map + exe-naam) en is de buffer ruim genoeg.
    /// Deze app wordt niet als single-file gepubliceerd (het SxS-manifest gebruikt geen <c>loadFrom</c>), dus de
    /// variabele heeft verder geen functie. Gebruik je ooit wél PublishSingleFile, haal dit dan weg.
    /// </para>
    /// </summary>
    private static void AvoidMrmHeapOverflow()
        => Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", null);
}
