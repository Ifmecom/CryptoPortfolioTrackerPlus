using CryptoPortfolioTracker.Enums;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure-statische validator voor trade-setup parameters.
/// Controleert of SL/TP logisch consistent zijn met de entry-prijs en richting.
///
/// Geen UI- of EF-afhankelijkheden — volledig unit-testbaar.
/// </summary>
public static class TradeSetupValidator
{
    /// <summary>Resultaat van een validatiecontrole.</summary>
    public sealed record ValidationResult(bool IsValid, string? Error = null)
    {
        public static ValidationResult Ok()                  => new(true);
        public static ValidationResult Fail(string message) => new(false, message);
    }

    /// <summary>Minimale acceptabele reward/risk-verhouding voordat we "krappe R/R" markeren.</summary>
    public const double MinHealthyRiskReward = 1.5;

    /// <summary>Uitkomst van een advies-controle (richting + R/R) voor weergave-tools.</summary>
    public sealed record AdviceCheck(bool IsValid, string Warning)
    {
        public static readonly AdviceCheck Ok = new(true, string.Empty);
    }

    /// <summary>
    /// Controleert een gegenereerd trade-advies (richting als tekst) en geeft een
    /// gebruikersvriendelijke waarschuwing terug:
    ///   • ongeldige/degenerate niveaus  → IsValid = false
    ///   • geldige niveaus maar R/R &lt; 1,5  → IsValid = true, waarschuwing "krappe R/R"
    /// Voor "Geen signaal" (of lege richting) is er niets te valideren → Ok.
    /// </summary>
    public static AdviceCheck CheckAdvice(
        string direction,
        double entry,
        double stopLoss,
        double takeProfit1,
        double takeProfit2,
        double riskReward1)
    {
        // Alleen Long/Short hebben niveaus om te valideren.
        if (!string.Equals(direction, "Long",  StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(direction, "Short", StringComparison.OrdinalIgnoreCase))
            return AdviceCheck.Ok;

        var side   = direction.Equals("Short", StringComparison.OrdinalIgnoreCase)
            ? OrderSide.Sell : OrderSide.Buy;
        var result = Validate(side, entry, stopLoss, takeProfit1, takeProfit2);

        if (!result.IsValid)
            return new AdviceCheck(false, result.Error ?? "Ongeldige SL/TP-niveaus.");

        if (riskReward1 > 0 && riskReward1 < MinHealthyRiskReward)
            return new AdviceCheck(true,
                $"Krappe R/R ({riskReward1:F2}:1) — onder de gezonde drempel van {MinHealthyRiskReward:F1}:1.");

        return AdviceCheck.Ok;
    }

    /// <summary>
    /// Valideert dat SL/TP-niveaus richtingsgewijs correct en niet-degenraat zijn
    /// ten opzichte van de opgegeven entry-prijs.
    ///
    /// Regels:
    ///   Long  (Buy):  SL &lt; entry,  TP &gt; entry,  TP2 &gt; TP1
    ///   Short (Sell): SL &gt; entry,  TP &lt; entry,  TP2 &lt; TP1
    ///
    /// Een waarde van 0 betekent "niet ingesteld" en wordt overgeslagen.
    /// </summary>
    /// <param name="side">Buy (Long) of Sell (Short).</param>
    /// <param name="entry">Effectieve entry-prijs (moet &gt; 0 zijn).</param>
    /// <param name="stopLoss">Stop-loss prijs (0 = niet ingesteld).</param>
    /// <param name="takeProfit">Take-profit 1 prijs (0 = niet ingesteld).</param>
    /// <param name="takeProfit2">Take-profit 2 prijs (0 = niet ingesteld).</param>
    public static ValidationResult Validate(
        OrderSide side,
        double    entry,
        double    stopLoss,
        double    takeProfit,
        double    takeProfit2 = 0)
    {
        if (entry <= 0)
            return ValidationResult.Fail("Entry-prijs moet groter zijn dan nul.");

        bool isLong = side == OrderSide.Buy;

        // ── Stop Loss ──────────────────────────────────────────────────────────
        if (stopLoss > 0)
        {
            if (stopLoss == entry)
                return ValidationResult.Fail(
                    $"Stop-loss ({Fmt(stopLoss)}) staat gelijk aan de entry — geen risico gedefinieerd.");

            if (isLong && stopLoss >= entry)
                return ValidationResult.Fail(
                    $"Long stop-loss ({Fmt(stopLoss)}) moet ONDER de entry ({Fmt(entry)}) liggen. " +
                    $"Controleer of je een absolute prijs hebt ingevoerd (niet een percentage).");

            if (!isLong && stopLoss <= entry)
                return ValidationResult.Fail(
                    $"Short stop-loss ({Fmt(stopLoss)}) moet BOVEN de entry ({Fmt(entry)}) liggen. " +
                    $"Controleer of je een absolute prijs hebt ingevoerd (niet een percentage).");
        }

        // ── Take Profit 1 ──────────────────────────────────────────────────────
        if (takeProfit > 0)
        {
            if (takeProfit == entry)
                return ValidationResult.Fail(
                    $"Take-profit ({Fmt(takeProfit)}) staat gelijk aan de entry — geen winst mogelijk.");

            if (isLong && takeProfit <= entry)
                return ValidationResult.Fail(
                    $"Long take-profit ({Fmt(takeProfit)}) moet BOVEN de entry ({Fmt(entry)}) liggen. " +
                    $"Controleer of je een absolute prijs hebt ingevoerd (niet een percentage).");

            if (!isLong && takeProfit >= entry)
                return ValidationResult.Fail(
                    $"Short take-profit ({Fmt(takeProfit)}) moet ONDER de entry ({Fmt(entry)}) liggen. " +
                    $"Controleer of je een absolute prijs hebt ingevoerd (niet een percentage).");
        }

        // ── Take Profit 2 (alleen gevalideerd als ook TP1 gezet is) ───────────
        if (takeProfit2 > 0 && takeProfit > 0)
        {
            if (isLong && takeProfit2 <= takeProfit)
                return ValidationResult.Fail(
                    $"TP2 ({Fmt(takeProfit2)}) moet BOVEN TP1 ({Fmt(takeProfit)}) liggen voor een Long.");

            if (!isLong && takeProfit2 >= takeProfit)
                return ValidationResult.Fail(
                    $"TP2 ({Fmt(takeProfit2)}) moet ONDER TP1 ({Fmt(takeProfit)}) liggen voor een Short.");
        }

        return ValidationResult.Ok();
    }

    /// <summary>
    /// Valideert SL/TP-niveaus voor een <b>reeds geopende</b> positie t.o.v. de <b>huidige koers</b>
    /// i.p.v. de entry. Voor een lopende trade hoeft de stop niet langer aan de entry-kant te liggen —
    /// je mag hem naar winst trekken (bv. een short-stop ónder de entry maar bóven de huidige koers).
    ///
    /// Regels (zodat elk niveau een nog-niet-geraakte order blijft):
    ///   Long  (Buy):  SL &lt; huidige koers,  TP &gt; huidige koers,  TP2 &gt; TP1
    ///   Short (Sell): SL &gt; huidige koers,  TP &lt; huidige koers,  TP2 &lt; TP1
    /// Een waarde van 0 betekent "niet ingesteld" en wordt overgeslagen.
    /// </summary>
    public static ValidationResult ValidateForOpenPosition(
        OrderSide side,
        double    currentPrice,
        double    stopLoss,
        double    takeProfit,
        double    takeProfit2 = 0)
    {
        if (currentPrice <= 0)
            return ValidationResult.Fail("Huidige koers onbekend — kan de niveaus niet valideren.");

        bool isLong = side == OrderSide.Buy;

        // ── Stop Loss — moet aan de verlieskant van de huidige koers liggen ─────
        if (stopLoss > 0)
        {
            if (isLong && stopLoss >= currentPrice)
                return ValidationResult.Fail(
                    $"Long stop-loss ({Fmt(stopLoss)}) moet ONDER de huidige koers ({Fmt(currentPrice)}) liggen, " +
                    $"anders sluit de positie direct.");

            if (!isLong && stopLoss <= currentPrice)
                return ValidationResult.Fail(
                    $"Short stop-loss ({Fmt(stopLoss)}) moet BOVEN de huidige koers ({Fmt(currentPrice)}) liggen, " +
                    $"anders sluit de positie direct.");
        }

        // ── Take Profit 1 — moet aan de winstkant van de huidige koers liggen ───
        if (takeProfit > 0)
        {
            if (isLong && takeProfit <= currentPrice)
                return ValidationResult.Fail(
                    $"Long take-profit ({Fmt(takeProfit)}) moet BOVEN de huidige koers ({Fmt(currentPrice)}) liggen.");

            if (!isLong && takeProfit >= currentPrice)
                return ValidationResult.Fail(
                    $"Short take-profit ({Fmt(takeProfit)}) moet ONDER de huidige koers ({Fmt(currentPrice)}) liggen.");
        }

        // ── Take Profit 2 (alleen gevalideerd als ook TP1 gezet is) ───────────
        if (takeProfit2 > 0 && takeProfit > 0)
        {
            if (isLong && takeProfit2 <= takeProfit)
                return ValidationResult.Fail(
                    $"TP2 ({Fmt(takeProfit2)}) moet BOVEN TP1 ({Fmt(takeProfit)}) liggen voor een Long.");

            if (!isLong && takeProfit2 >= takeProfit)
                return ValidationResult.Fail(
                    $"TP2 ({Fmt(takeProfit2)}) moet ONDER TP1 ({Fmt(takeProfit)}) liggen voor een Short.");
        }

        return ValidationResult.Ok();
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static string Fmt(double price) => price switch
    {
        >= 1_000 => $"{price:#,0.##}",
        >= 1     => $"{price:F4}",
        >= 0.01  => $"{price:F6}",
        _        => $"{price:F8}",
    };
}
