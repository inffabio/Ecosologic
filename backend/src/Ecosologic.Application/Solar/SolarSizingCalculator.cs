using Ecosologic.Domain.Solar;

namespace Ecosologic.Application.Solar;

/// <summary>
/// Motor determinístico do dimensionamento técnico. Não depende de banco, HTTP ou relógio.
///
/// Fórmulas (premissas exatas):
/// - Perdas totais = soma das perdas individuais (aditiva), mesma totalização da planilha.
/// - HSPk[m] = HSP[m] * K[m] (horas de sol pleno efetivas, diárias).
/// - Geração mensal[m] = HSPk[m] * kWp instalado * dias/mês * (1 - perdas totais) * derating.
///   dias/mês = 30 (constante, mesma simplificação da planilha).
/// - kWp alvo = (consumo médio diário / HSPk médio) * (1 + perdas totais) * sobredimensionamento / derating.
///   O markup (1 + perdas) é a aproximação linear usada pela planilha (não 1/(1-perdas)); o derating
///   divide a meta (geração alvo), enquanto a geração real multiplica por derating — sem dupla contagem.
/// - Módulos = arredondamento para cima (ceiling) de (kWp alvo * 1000 / potência do módulo).
/// - kWp instalado = módulos * potência do módulo / 1000.
/// - Energia compensável (técnica, sem Fio B) = min(geração, consumo) por mês.
/// </summary>
public sealed class SolarSizingCalculator
{
    public const double DaysPerMonth = 30.0;
    public const string EngineVersion = "1.0.0";

    public SolarSizingResult Calculate(SolarSizingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var consumption = input.MonthlyConsumptionKWh;
        var hsp = input.MonthlyHsp;
        var kFactor = input.MonthlyKFactor;
        var module = input.Module;

        var totalLosses = input.Losses.TotalLosses;

        // Fator de perdas clampado: perdas totais >= 100% zeram a geração, nunca produzem
        // valores negativos (resultado seguro + alerta impeditivo emitido em BuildAlerts).
        var lossFactor = Math.Max(0.0, 1.0 - totalLosses);
        var derating = input.DeratingFactor;

        var hspk = new double[12];
        var hspkSum = 0.0;
        for (var i = 0; i < 12; i++)
        {
            hspk[i] = RequireFinite(hsp[i] * kFactor[i], $"HSPk[{i}]");
            hspkSum += hspk[i];
        }

        var avgHspk = RequireFinite(hspkSum / 12.0, "HSPk médio");

        var annualConsumption = 0.0;
        for (var i = 0; i < 12; i++)
            annualConsumption += consumption[i];

        var avgDailyConsumption = RequireFinite(annualConsumption / 12.0 / DaysPerMonth, "consumo médio diário");

        var targetKwp = 0.0;
        var moduleCount = 0;
        if (avgHspk > 0)
        {
            // O derating entra no dimensionamento alvo (geração meta), dividindo a meta;
            // a geração real multiplica por derating, evitando dupla contagem.
            targetKwp = RequireFinite(
                avgDailyConsumption / avgHspk * (1.0 + totalLosses) * input.OversizingFactor / derating,
                "kWp alvo");

            var requiredModules = RequireFinite(targetKwp * 1000.0 / module.PowerWp, "módulos necessários");

            var moduleCountDouble = Math.Ceiling(requiredModules);
            if (moduleCountDouble > int.MaxValue)
                throw new InvalidOperationException("Quantidade de módulos excede o limite representável; verifique as entradas do dimensionamento.");

            moduleCount = (int)moduleCountDouble;
        }

        var installedKwp = RequireFinite(moduleCount * module.PowerWp / 1000.0, "kWp instalado");

        var generation = new double[12];
        var annualGeneration = 0.0;
        for (var i = 0; i < 12; i++)
        {
            generation[i] = RequireFinite(
                hspk[i] * installedKwp * DaysPerMonth * lossFactor * derating,
                $"geração[{i}]");
            annualGeneration += generation[i];
        }

        var averageGeneration = RequireFinite(annualGeneration / 12.0, "geração média");

        var compensable = new double[12];
        var balance = new double[12];
        var annualCompensable = 0.0;
        var surplus = 0.0;
        var deficit = 0.0;
        var worstMonthIndex = 0;
        var worstBalance = double.MaxValue;

        for (var i = 0; i < 12; i++)
        {
            compensable[i] = Math.Min(generation[i], consumption[i]);
            annualCompensable += compensable[i];

            balance[i] = generation[i] - consumption[i];
            if (balance[i] > 0)
                surplus += balance[i];
            else
                deficit += -balance[i];

            if (balance[i] < worstBalance)
            {
                worstBalance = balance[i];
                worstMonthIndex = i;
            }
        }

        var worstDeficit = Math.Max(0.0, -worstBalance);
        var coverage = RequireFinite(
            annualConsumption > 0 ? annualGeneration / annualConsumption : 0.0,
            "cobertura");

        var (modulesPerString, stringCount) = CompleteConfiguration(input, moduleCount);

        var alerts = BuildAlerts(input, moduleCount, modulesPerString, stringCount, installedKwp, totalLosses, annualGeneration);

        return SolarSizingResult.Create(
            Array.AsReadOnly(generation),
            annualGeneration,
            averageGeneration,
            annualConsumption,
            totalLosses,
            targetKwp,
            installedKwp,
            moduleCount,
            modulesPerString,
            stringCount,
            Array.AsReadOnly(compensable),
            annualCompensable,
            coverage,
            Array.AsReadOnly(balance),
            surplus,
            deficit,
            worstMonthIndex + 1,
            generation[worstMonthIndex],
            consumption[worstMonthIndex],
            worstDeficit,
            alerts.AsReadOnly());
    }

    private static (int ModulesPerString, int StringCount) CompleteConfiguration(
        SolarSizingInput input, int moduleCount)
    {
        if (input.ModulesPerString.HasValue && input.StringCount.HasValue)
            return (input.ModulesPerString.Value, input.StringCount.Value);

        if (moduleCount <= 0)
            return (0, 0);

        if (input.ModulesPerString.HasValue)
            return (input.ModulesPerString.Value, DivideRoundUp(moduleCount, input.ModulesPerString.Value));

        if (input.StringCount.HasValue)
            return (DivideRoundUp(moduleCount, input.StringCount.Value), input.StringCount.Value);

        return AutoConfigure(input, moduleCount);
    }

    // Configuração automática: deriva módulos/string e strings de modo que o produto seja
    // EXATAMENTE moduleCount (nunca sobre/subaloca), respeitando o limite de tensão do
    // inversor. Usa o maior divisor de moduleCount que respeita o limite, maximizando
    // módulos por string (menos strings, menos cabeamento).
    private static (int ModulesPerString, int StringCount) AutoConfigure(SolarSizingInput input, int moduleCount)
    {
        var mpsMax = MaxModulesPerStringByVoltage(input, moduleCount);
        var modulesPerString = LargestDivisorAtMost(moduleCount, mpsMax);
        return (modulesPerString, moduleCount / modulesPerString);
    }

    private static int MaxModulesPerStringByVoltage(SolarSizingInput input, int moduleCount)
    {
        if (input.Inverter is null)
            return moduleCount;

        var inverter = input.Inverter;
        var module = input.Module;

        var byVoltage = SafeFloorToInt(inverter.MpptVoltageMax, module.Vmp, "MpptVoltageMax / Vmp");
        var byVoc = SafeFloorToInt(inverter.MaxInputVoltage, module.Voc, "MaxInputVoltage / Voc");

        var limit = Math.Min(byVoltage, byVoc);
        if (limit < 1)
            limit = 1;

        return Math.Min(limit, moduleCount);
    }

    // Maior divisor de n que não excede max, em O(sqrt(n)): percorre os pares (i, n/i)
    // até a raiz quadrada de n e mantém o maior divisor válido. Evita o loop anterior
    // proporcional a max (que podia ser da ordem de moduleCount).
    private static int LargestDivisorAtMost(int n, int max)
    {
        if (max >= n)
            return n;
        if (max < 1)
            return 1;

        var best = 1;
        var limit = (int)Math.Floor(Math.Sqrt(n));
        for (var i = 1; i <= limit; i++)
        {
            if (n % i != 0)
                continue;

            if (i <= max && i > best)
                best = i;

            var coDivisor = n / i;
            if (coDivisor <= max && coDivisor > best)
                best = coDivisor;
        }

        return best;
    }

    private static int DivideRoundUp(int total, int divisor) =>
        (int)(((long)total + divisor - 1) / divisor);

    private static int SafeFloorToInt(double numerator, double denominator, string label)
    {
        RequireFinite(numerator, $"{label} (numerador)");
        RequireFinite(denominator, $"{label} (denominador)");

        var quotient = RequireFinite(numerator / denominator, $"quociente {label}");

        if (quotient > int.MaxValue)
            throw new InvalidOperationException(
                $"O quociente '{label}' ({quotient:G}) excede o intervalo seguro para conversão em int; verifique as tensões do módulo e do inversor.");

        return (int)Math.Floor(quotient);
    }

    private static List<SolarSizingAlert> BuildAlerts(
        SolarSizingInput input,
        int moduleCount,
        int modulesPerString,
        int stringCount,
        double installedKwp,
        double totalLosses,
        double annualGeneration)
    {
        var alerts = new List<SolarSizingAlert>();

        if (totalLosses >= 1.0)
            alerts.Add(Alert("PerdasTotaisAcimaDe100", SolarSizingAlertSeverity.Blocking,
                "Perdas totais atingem 100% ou mais; não há geração útil."));

        if (annualGeneration <= 0)
            alerts.Add(Alert("SemGeracao", SolarSizingAlertSeverity.Blocking,
                "O sistema não gera energia com as entradas fornecidas (HSP, potência ou perdas zeram a geração)."));

        var requiredArea = RequireFinite(moduleCount * input.Module.AreaM2, "área necessária");
        if (requiredArea > input.AvailableAreaM2)
            alerts.Add(Alert("AreaInsuficiente", SolarSizingAlertSeverity.Blocking,
                $"Área necessária ({requiredArea:F2} m²) excede a área disponível ({input.AvailableAreaM2:F2} m²)."));
        else if (requiredArea > 0.9 * input.AvailableAreaM2)
            alerts.Add(Alert("AreaApertada", SolarSizingAlertSeverity.Warning,
                "A área necessária ocupa mais de 90% da área disponível."));

        if (input.Inverter is not null)
            AddInverterAlerts(alerts, input, modulesPerString, stringCount, installedKwp);

        // Qualquer configuração elétrica fornecida (módulos por string E/OU número de strings)
        // é completada e deve alocar exatamente o número de módulos dimensionado. Divergência
        // (sobrealocação ou subalocação) é incompatibilidade impeditiva: a potência reportada é
        // SEMPRE a do dimensionamento, nunca a da configuração elétrica. A configuração
        // automática (nenhum valor fornecido) é consistente por construção (produto == moduleCount).
        if (input.ModulesPerString.HasValue || input.StringCount.HasValue)
        {
            var allocatedModules = (long)modulesPerString * stringCount;
            if (allocatedModules != moduleCount)
                alerts.Add(Alert("ConfiguracaoEletricaDivergente", SolarSizingAlertSeverity.Blocking,
                    $"A configuração elétrica ({modulesPerString} módulo(s)/string × {stringCount} string(s) = {allocatedModules} módulo(s)) não corresponde aos {moduleCount} módulo(s) dimensionado(s)."));
        }

        return alerts;
    }

    private static void AddInverterAlerts(
        List<SolarSizingAlert> alerts,
        SolarSizingInput input,
        int modulesPerString,
        int stringCount,
        double installedKwp)
    {
        var inverter = input.Inverter!;
        var module = input.Module;

        var stringVmp = RequireFinite(modulesPerString * module.Vmp, "tensão Vmp da string");
        var stringVoc = RequireFinite(modulesPerString * module.Voc, "tensão Voc da string");

        if (stringVmp > inverter.MpptVoltageMax)
            alerts.Add(Alert("TensaoStringAcimaMpptMax", SolarSizingAlertSeverity.Blocking,
                $"Tensão Vmp da string ({stringVmp:F1} V) excede o máximo do MPPT ({inverter.MpptVoltageMax:F1} V)."));
        else if (stringVmp < inverter.MpptVoltageMin)
            alerts.Add(Alert("TensaoStringAbaixoMpptMin", SolarSizingAlertSeverity.Warning,
                $"Tensão Vmp da string ({stringVmp:F1} V) fica abaixo do mínimo do MPPT ({inverter.MpptVoltageMin:F1} V)."));

        if (stringVoc > inverter.MaxInputVoltage)
            alerts.Add(Alert("TensaoVocAcimaMaxInversor", SolarSizingAlertSeverity.Blocking,
                $"Tensão Voc da string ({stringVoc:F1} V) excede a tensão máxima do inversor ({inverter.MaxInputVoltage:F1} V)."));

        // Strings em paralelo por MPPT (distribuição balanceada). A tensão por string não soma
        // em paralelo; a corrente, sim. Por isso a corrente por MPPT multiplica Imp/Isc pela
        // quantidade de strings paralelas naquele MPPT.
        var stringsPerMppt = stringCount > 0
            ? Math.Max(1, (int)Math.Ceiling((double)stringCount / inverter.MpptCount))
            : 1;

        if (stringCount > (long)inverter.MpptCount * inverter.MaxStringsPerMppt)
            alerts.Add(Alert("NumeroStringsAcimaMppt", SolarSizingAlertSeverity.Blocking,
                $"Número de strings ({stringCount}) excede a capacidade do inversor ({inverter.MpptCount} MPPT(s) × {inverter.MaxStringsPerMppt} string(s) por MPPT)."));

        var currentImp = RequireFinite(stringsPerMppt * module.Imp, "corrente Imp por MPPT");
        if (currentImp > inverter.MaxInputCurrent)
            alerts.Add(Alert("CorrenteAcimaMaxInversor", SolarSizingAlertSeverity.Blocking,
                $"Corrente Imp por MPPT ({currentImp:F2} A, {stringsPerMppt} string(s) em paralelo) excede a corrente máxima por MPPT do inversor ({inverter.MaxInputCurrent:F2} A)."));

        var currentIsc = RequireFinite(stringsPerMppt * module.Isc, "corrente Isc por MPPT");
        if (currentIsc > inverter.MaxInputCurrent)
            alerts.Add(Alert("CorrenteCurtoAcimaMaxInversor", SolarSizingAlertSeverity.Blocking,
                $"Corrente de curto-circuito Isc por MPPT ({currentIsc:F2} A, {stringsPerMppt} string(s) em paralelo) excede a corrente máxima por MPPT do inversor ({inverter.MaxInputCurrent:F2} A)."));

        var inverterKw = inverter.NominalPowerW / 1000.0;
        if (inverterKw > 0)
        {
            var ratio = RequireFinite(installedKwp / inverterKw, "fator de dimensionamento global");
            if (ratio is < 0.75 or > 1.30)
                alerts.Add(Alert("FatorDimensionamentoGlobalForaDaFaixa", SolarSizingAlertSeverity.Warning,
                    $"Fator de dimensionamento global (Pcc/Pca = {ratio:F2}) fora da faixa recomendada de 0,75 a 1,30."));
        }
    }

    private static double RequireFinite(double value, string label)
    {
        if (!double.IsFinite(value))
            throw new InvalidOperationException(
                $"Cálculo intermediário '{label}' resultou em valor não finito (overflow/NaN); verifique as entradas do dimensionamento.");
        return value;
    }

    private static SolarSizingAlert Alert(string code, SolarSizingAlertSeverity severity, string message) =>
        SolarSizingAlert.Create(code, severity, message);
}
