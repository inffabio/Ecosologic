using Ecosologic.Application.Solar;
using Ecosologic.Domain.Solar;

namespace Ecosologic.Api.Tests;

public class SolarSizingCalculatorTests
{
    private static readonly SolarSizingCalculator Calculator = new();

    private static readonly double[] RefConsumption =
        [750, 750, 750, 750, 750, 750, 750, 750, 750, 750, 750, 750];

    // HSP mensal (diário) da planilha PlanilhaDimensionamento750.xlsx (aba "Irradiação Solar", linha 13).
    private static readonly double[] RefHsp =
        [6.18, 6.38, 5.15, 4.44, 3.59, 3.32, 3.35, 4.22, 4.41, 5.08, 5.22, 6.05];

    // Fator K (latitude 23, inclinação 10) da aba "Correção K" (linha 441).
    private static readonly double[] RefK =
        [0.99, 1.01, 1.05, 1.08, 1.10, 1.10, 1.09, 1.07, 1.04, 1.01, 0.99, 0.98];

    // Perdas da planilha: sombreamento 0, sujeira 0.02, tolerância 0, mismatch 0,
    // temperatura 0.112 (média do Rio de Janeiro), CC 0.01, MPPT 0.02, inversor 0.04, CA 0.01.
    // Total = 0.212.
    private static readonly SolarLosses RefLosses = SolarLosses.Create(
        sombreamento: 0.0,
        sujeira: 0.02,
        tolerancia: 0.0,
        mismatch: 0.0,
        temperatura: 0.112,
        cc: 0.01,
        mppt: 0.02,
        inversor: 0.04,
        ca: 0.01);

    // Módulo de referência coerente: Vmp × Imp (41.3 × 16.9 = 697.97 W) ≈ PowerWp (700 W),
    // Voc >= Vmp e Isc >= Imp, dentro da tolerância documentada de 5%.
    private static SolarModuleSpec RefModule() =>
        SolarModuleSpec.Create(powerWp: 700, voc: 49.2, vmp: 41.3, isc: 17.5, imp: 16.9, areaM2: 2.2);

    private static SolarInverterSpec RefInverter(
        double nominalPowerW = 6000,
        double mpptVoltageMin = 80,
        double mpptVoltageMax = 480,
        double maxInputVoltage = 600,
        double maxInputCurrent = 20,
        int mpptCount = 2,
        int maxStringsPerMppt = 1) =>
        SolarInverterSpec.Create(
            nominalPowerW, mpptVoltageMin, mpptVoltageMax, maxInputVoltage, maxInputCurrent, mpptCount, maxStringsPerMppt);

    private static SolarSizingInput ReferenceInput(
        SolarInverterSpec? inverter = null,
        int? modulesPerString = null,
        int? stringCount = null,
        double deratingFactor = 1.0) =>
        SolarSizingInput.Create(
            monthlyConsumptionKWh: RefConsumption,
            monthlyHsp: RefHsp,
            monthlyKFactor: RefK,
            module: RefModule(),
            losses: RefLosses,
            orientation: "Norte",
            inclinationDegrees: 10,
            availableAreaM2: 50.0,
            oversizingFactor: 1.0,
            deratingFactor: deratingFactor,
            inverter: inverter,
            modulesPerString: modulesPerString,
            stringCount: stringCount);

    // --- Regressão (caso de referência da planilha) ---

    [Fact]
    public void Reference_case_reproduces_spreadsheet_kwp_and_generation()
    {
        var result = Calculator.Calculate(ReferenceInput());

        Assert.Equal(9, result.ModuleCount);
        Assert.Equal(6.3, result.InstalledKwp, 6);
        Assert.InRange(result.TargetKwp, 6.0, 6.3);

        // Regressão contra a planilha (PlanilhaDimensionamento750.xlsx), com tolerância ABSOLUTA
        // apertada. Divergência exata documentada no spec (motor vs planilha):
        //   média: 736.5507 vs 736.5840 -> -0.0333 kWh (-0.005%)
        //   anual: 8838.6078 vs 8839.0100 -> -0.4022 kWh (-0.005%)
        // Causa: a planilha usa VLOOKUP deslocado na perda por temperatura (Janeiro–Abril
        // usam a coluna de Abril); o motor recebe a temperatura média 0.112 como entrada.
        Assert.Equal(736.584, result.AverageGenerationKWh, tolerance: 0.05);
        Assert.Equal(8839.01, result.AnnualGenerationKWh, tolerance: 0.5);
    }

    [Fact]
    public void Reference_case_reports_surplus_deficit_and_worst_month()
    {
        var result = Calculator.Calculate(ReferenceInput());

        Assert.True(result.SurplusKWh > 0);
        Assert.True(result.DeficitKWh > 0);
        // Pior mês: julho (menor geração, ~543.8 kWh).
        Assert.Equal(7, result.WorstMonthIndex);

        // Identidades: geração = compensável + excedente; consumo = compensável + déficit.
        Assert.Equal(result.AnnualGenerationKWh, result.AnnualEnergyCompensableKWh + result.SurplusKWh, 6);
        Assert.Equal(result.AnnualConsumptionKWh, result.AnnualEnergyCompensableKWh + result.DeficitKWh, 6);
    }

    // --- Perdas ---

    [Fact]
    public void Losses_are_additive_and_reduce_generation_linearly()
    {
        var tenPercent = SolarLosses.Create(0.10, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Equal(0.10, tenPercent.TotalLosses);

        var noLosses = SolarLosses.Create(0, 0, 0, 0, 0, 0, 0, 0, 0);
        var baseResult = Calculator.Calculate(WithLosses(noLosses));
        var reduced = Calculator.Calculate(WithLosses(tenPercent));

        // O rendimento específico (kWh/kWp) escala linearmente com (1 - perdas).
        var baseYield = baseResult.AverageGenerationKWh / baseResult.InstalledKwp;
        var reducedYield = reduced.AverageGenerationKWh / reduced.InstalledKwp;

        Assert.Equal(baseYield * 0.9, reducedYield, 6);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Individual_losses_must_be_between_zero_and_one(double loss)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarLosses.Create(0, loss, 0, 0, 0, 0, 0, 0, 0));
    }

    // --- 12 meses ---

    [Fact]
    public void Consumption_must_have_exactly_12_months()
    {
        Assert.Throws<ArgumentException>(() =>
            SolarSizingInput.Create(
                new double[11], RefHsp, RefK, RefModule(), RefLosses, "Norte", 10, 50.0));

        Assert.Throws<ArgumentException>(() =>
            SolarSizingInput.Create(
                new double[13], RefHsp, RefK, RefModule(), RefLosses, "Norte", 10, 50.0));
    }

    [Fact]
    public void Hsp_must_have_exactly_12_months()
    {
        Assert.Throws<ArgumentException>(() =>
            SolarSizingInput.Create(
                RefConsumption, new double[11], RefK, RefModule(), RefLosses, "Norte", 10, 50.0));
    }

    [Fact]
    public void K_factor_must_have_exactly_12_months()
    {
        Assert.Throws<ArgumentException>(() =>
            SolarSizingInput.Create(
                RefConsumption, RefHsp, new double[11], RefModule(), RefLosses, "Norte", 10, 50.0));
    }

    // --- NaN / Infinity / negativos ---

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Rejects_non_finite_hsp(double value)
    {
        var hsp = new double[12];
        Array.Fill(hsp, 5.0);
        hsp[0] = value;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarSizingInput.Create(RefConsumption, hsp, RefK, RefModule(), RefLosses, "Norte", 10, 50.0));
    }

    [Fact]
    public void Rejects_negative_consumption()
    {
        var consumption = new double[12];
        Array.Fill(consumption, 750.0);
        consumption[3] = -1;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarSizingInput.Create(consumption, RefHsp, RefK, RefModule(), RefLosses, "Norte", 10, 50.0));
    }

    [Fact]
    public void Rejects_negative_module_power() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarModuleSpec.Create(powerWp: -700, voc: 49.2, vmp: 41.3, isc: 17.5, imp: 16.9, areaM2: 2.2));

    [Fact]
    public void Module_rejects_voc_below_vmp() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarModuleSpec.Create(powerWp: 700, voc: 40, vmp: 41.3, isc: 17.5, imp: 16.9, areaM2: 2.2));

    [Fact]
    public void Module_rejects_isc_below_imp() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarModuleSpec.Create(powerWp: 700, voc: 49.2, vmp: 41.3, isc: 16.0, imp: 16.9, areaM2: 2.2));

    [Fact]
    public void Module_rejects_power_incoherent_with_vmp_times_imp() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarModuleSpec.Create(powerWp: 900, voc: 49.2, vmp: 41.3, isc: 17.5, imp: 16.9, areaM2: 2.2));

    [Fact]
    public void Rejects_non_positive_area() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarSizingInput.Create(RefConsumption, RefHsp, RefK, RefModule(), RefLosses, "Norte", 10, 0));

    // --- Arredondamento ---

    [Fact]
    public void Module_count_rounds_up()
    {
        var hsp = new double[12];
        Array.Fill(hsp, 5.0);
        var k = new double[12];
        Array.Fill(k, 1.0);
        var consumption = new double[12];
        Array.Fill(consumption, 760.0);

        var input = SolarSizingInput.Create(
            consumption, hsp, k,
            SolarModuleSpec.Create(powerWp: 1000, voc: 50, vmp: 42, isc: 25, imp: 23.8, areaM2: 2.5),
            SolarLosses.Create(0, 0, 0, 0, 0, 0, 0, 0, 0),
            "Norte", 10, 100.0);

        var result = Calculator.Calculate(input);

        // target = 760/30/5 = 5.0667 kWp => 5.0667 módulos => arredonda para 6.
        Assert.InRange(result.TargetKwp, 5.0, 5.1);
        Assert.Equal(6, result.ModuleCount);
        Assert.Equal(6.0, result.InstalledKwp, 6);
    }

    [Fact]
    public void Installed_kwp_equals_module_count_times_module_power()
    {
        var result = Calculator.Calculate(ReferenceInput());

        Assert.Equal(9 * 700 / 1000.0, result.InstalledKwp, 6);
    }

    // --- Excesso / déficit ---

    [Fact]
    public void Compensable_is_capped_by_consumption()
    {
        var result = Calculator.Calculate(ReferenceInput());

        for (var i = 0; i < 12; i++)
            Assert.InRange(result.MonthlyEnergyCompensableKWh[i], 0, RefConsumption[i]);
    }

    // --- Derating ---

    [Fact]
    public void Derating_scales_specific_yield_proportionally()
    {
        var full = Calculator.Calculate(ReferenceInput());
        var derated = Calculator.Calculate(ReferenceInput(deratingFactor: 0.9));

        // O rendimento específico (kWh/kWp) escala linearmente com o derating.
        var fullYield = full.AnnualGenerationKWh / full.InstalledKwp;
        var deratedYield = derated.AnnualGenerationKWh / derated.InstalledKwp;

        Assert.Equal(fullYield * 0.9, deratedYield, 6);
    }

    [Fact]
    public void Derating_enters_target_sizing_without_double_counting_generation()
    {
        var full = Calculator.Calculate(ReferenceInput());
        var derated = Calculator.Calculate(ReferenceInput(deratingFactor: 0.9));

        // O derating entra no dimensionamento alvo (geração meta): kWp alvo cresce em 1/derating.
        Assert.Equal(full.TargetKwp / 0.9, derated.TargetKwp, 6);

        // O kWp instalado é suficiente para a meta, mesmo com derating de 0,9.
        Assert.True(derated.InstalledKwp >= derated.TargetKwp);

        // Sem dupla contagem: a geração-meta é preservada (derating compensado pelo kWp extra).
        Assert.InRange(derated.AnnualGenerationKWh, full.AnnualGenerationKWh * 0.999, full.AnnualGenerationKWh * 1.001);
    }

    // --- Alertas impeditivos / não impeditivos ---

    [Fact]
    public void Insufficient_area_is_blocking()
    {
        var input = SolarSizingInput.Create(
            RefConsumption, RefHsp, RefK, RefModule(), RefLosses, "Norte", 10, availableAreaM2: 5.0);

        var result = Calculator.Calculate(input);

        Assert.True(result.HasBlockingAlert);
        Assert.Contains(result.Alerts, a => a.Code == "AreaInsuficiente" && a.Severity == SolarSizingAlertSeverity.Blocking);
    }

    [Fact]
    public void String_voltage_above_mppt_max_is_blocking()
    {
        var inverter = RefInverter(mpptVoltageMin: 0, mpptVoltageMax: 40);
        var result = Calculator.Calculate(ReferenceInput(inverter: inverter, modulesPerString: 1));

        Assert.Contains(result.Alerts, a => a.Code == "TensaoStringAcimaMpptMax" && a.Severity == SolarSizingAlertSeverity.Blocking);
    }

    [Fact]
    public void String_voc_above_inverter_max_is_blocking()
    {
        var inverter = RefInverter(mpptVoltageMin: 0, mpptVoltageMax: 60, maxInputVoltage: 40);
        var result = Calculator.Calculate(ReferenceInput(inverter: inverter, modulesPerString: 1));

        Assert.Contains(result.Alerts, a => a.Code == "TensaoVocAcimaMaxInversor" && a.Severity == SolarSizingAlertSeverity.Blocking);
    }

    [Fact]
    public void Too_many_strings_is_blocking()
    {
        var inverter = RefInverter(mpptCount: 1);
        var result = Calculator.Calculate(ReferenceInput(inverter: inverter, modulesPerString: 5, stringCount: 2));

        Assert.Contains(result.Alerts, a => a.Code == "NumeroStringsAcimaMppt" && a.Severity == SolarSizingAlertSeverity.Blocking);
    }

    [Fact]
    public void Current_above_inverter_max_is_blocking()
    {
        var inverter = RefInverter(maxInputCurrent: 10);
        var result = Calculator.Calculate(ReferenceInput(inverter: inverter));

        Assert.Contains(result.Alerts, a => a.Code == "CorrenteAcimaMaxInversor" && a.Severity == SolarSizingAlertSeverity.Blocking);
    }

    [Fact]
    public void Parallel_strings_per_mppt_increase_current_and_are_checked()
    {
        var inverter = RefInverter(mpptCount: 1, maxInputCurrent: 20, maxStringsPerMppt: 2);
        var result = Calculator.Calculate(ReferenceInput(inverter: inverter, modulesPerString: 1, stringCount: 2));

        // 2 strings em paralelo no único MPPT: corrente = 2 × Imp = 33.8 A > 20 A.
        Assert.Contains(result.Alerts, a => a.Code == "CorrenteAcimaMaxInversor" && a.Severity == SolarSizingAlertSeverity.Blocking);
    }

    [Fact]
    public void Parallel_strings_within_limit_are_not_blocking()
    {
        var inverter = RefInverter(mpptCount: 1, maxInputCurrent: 40, maxStringsPerMppt: 2);
        var result = Calculator.Calculate(ReferenceInput(inverter: inverter, modulesPerString: 5, stringCount: 2));

        // 2 strings em 1 MPPT, dentro do limite de strings por MPPT e da corrente (2 × 16.9 = 33.8 <= 40).
        Assert.DoesNotContain(result.Alerts, a => a.Code == "NumeroStringsAcimaMppt");
        Assert.DoesNotContain(result.Alerts, a => a.Code == "CorrenteAcimaMaxInversor");
    }

    [Fact]
    public void Explicit_config_product_overflow_is_computed_in_long_and_blocking()
    {
        var inverter = RefInverter();
        var result = Calculator.Calculate(ReferenceInput(inverter: inverter, modulesPerString: 50000, stringCount: 50000));

        // 50000 × 50000 estoura int; o produto é calculado em long e não estoura,
        // resultando no alerta impeditivo correto (não em falso negativo de alocação).
        Assert.Contains(result.Alerts, a =>
            a.Code == "ConfiguracaoEletricaDivergente" && a.Severity == SolarSizingAlertSeverity.Blocking);
    }

    [Fact]
    public void Automatic_config_with_extreme_mppt_voltage_throws_instead_of_silent_overflow()
    {
        var inverter = RefInverter(mpptVoltageMin: 0, mpptVoltageMax: double.MaxValue);

        Assert.Throws<InvalidOperationException>(() => Calculator.Calculate(ReferenceInput(inverter: inverter)));
    }

    [Fact]
    public void Automatic_config_with_extreme_input_voltage_throws_instead_of_silent_overflow()
    {
        var inverter = RefInverter(mpptVoltageMin: 0, mpptVoltageMax: 480, maxInputVoltage: double.MaxValue);

        Assert.Throws<InvalidOperationException>(() => Calculator.Calculate(ReferenceInput(inverter: inverter)));
    }

    [Fact]
    public void Automatic_config_with_extreme_mppt_voltage_uses_explicit_config_without_overflow()
    {
        // Com modulesPerString explícito, a divisão por tensão não é executada: sem estouro.
        var inverter = RefInverter(mpptVoltageMin: 0, mpptVoltageMax: double.MaxValue);
        var result = Calculator.Calculate(ReferenceInput(inverter: inverter, modulesPerString: 9, stringCount: 1));

        Assert.DoesNotContain(result.Alerts, a => a.Code == "ConfiguracaoEletricaDivergente");
    }

    [Fact]
    public void Non_finite_target_sizing_throws_instead_of_producing_invalid_int()
    {
        var hsp = new double[12];
        Array.Fill(hsp, 1e-308);
        var k = new double[12];
        Array.Fill(k, 1.0);
        var consumption = new double[12];
        Array.Fill(consumption, 750.0);

        var input = SolarSizingInput.Create(consumption, hsp, k, RefModule(), RefLosses, "Norte", 10, 50.0);

        Assert.Throws<InvalidOperationException>(() => Calculator.Calculate(input));
    }

    [Fact]
    public void Hsp_sum_overflow_throws_instead_of_silent_zero_modules()
    {
        var hsp = new double[12];
        Array.Fill(hsp, 1e308);
        var k = new double[12];
        Array.Fill(k, 1.0);

        var input = SolarSizingInput.Create(RefConsumption, hsp, k, RefModule(), RefLosses, "Norte", 10, 50.0);

        Assert.Throws<InvalidOperationException>(() => Calculator.Calculate(input));
    }

    [Fact]
    public void Annual_generation_overflow_throws_instead_of_silent_infinity()
    {
        var hsp = new double[12];
        Array.Fill(hsp, 1e307);
        var k = new double[12];
        Array.Fill(k, 1.0);

        var input = SolarSizingInput.Create(RefConsumption, hsp, k, RefModule(), RefLosses, "Norte", 10, 50.0);

        Assert.Throws<InvalidOperationException>(() => Calculator.Calculate(input));
    }

    [Fact]
    public void Module_power_extreme_that_overflows_intermediates_throws()
    {
        var input = SolarSizingInput.Create(
            RefConsumption, RefHsp, RefK,
            SolarModuleSpec.Create(powerWp: double.MaxValue, voc: double.MaxValue, vmp: double.MaxValue, isc: 1.0, imp: 1.0, areaM2: double.MaxValue),
            RefLosses, "Norte", 10, 50.0);

        Assert.Throws<InvalidOperationException>(() => Calculator.Calculate(input));
    }

    [Fact]
    public void Reference_result_values_are_all_finite()
    {
        var result = Calculator.Calculate(ReferenceInput());

        Assert.All(result.MonthlyGenerationKWh, g => Assert.True(double.IsFinite(g)));
        Assert.All(result.MonthlyEnergyCompensableKWh, c => Assert.True(double.IsFinite(c)));
        Assert.All(result.MonthlyBalanceKWh, b => Assert.True(double.IsFinite(b)));
        Assert.True(double.IsFinite(result.AnnualGenerationKWh));
        Assert.True(double.IsFinite(result.AverageGenerationKWh));
        Assert.True(double.IsFinite(result.AnnualConsumptionKWh));
        Assert.True(double.IsFinite(result.TotalLosses));
        Assert.True(double.IsFinite(result.TargetKwp));
        Assert.True(double.IsFinite(result.InstalledKwp));
        Assert.True(double.IsFinite(result.AnnualEnergyCompensableKWh));
        Assert.True(double.IsFinite(result.Coverage));
        Assert.True(double.IsFinite(result.SurplusKWh));
        Assert.True(double.IsFinite(result.DeficitKWh));
        Assert.True(double.IsFinite(result.WorstMonthGenerationKWh));
        Assert.True(double.IsFinite(result.WorstMonthConsumptionKWh));
        Assert.True(double.IsFinite(result.WorstMonthDeficitKWh));
    }

    [Fact]
    public void Explicit_config_2x5_mismatching_9_modules_is_blocking()
    {
        var result = Calculator.Calculate(ReferenceInput(modulesPerString: 2, stringCount: 5));

        Assert.Contains(result.Alerts, a =>
            a.Code == "ConfiguracaoEletricaDivergente" && a.Severity == SolarSizingAlertSeverity.Blocking);
    }

    [Fact]
    public void Explicit_config_under_allocating_modules_is_blocking()
    {
        var result = Calculator.Calculate(ReferenceInput(modulesPerString: 2, stringCount: 3));

        Assert.Contains(result.Alerts, a =>
            a.Code == "ConfiguracaoEletricaDivergente" && a.Severity == SolarSizingAlertSeverity.Blocking);
    }

    [Fact]
    public void Explicit_config_matching_module_count_is_not_blocking()
    {
        var result = Calculator.Calculate(ReferenceInput(modulesPerString: 3, stringCount: 3));

        Assert.DoesNotContain(result.Alerts, a => a.Code == "ConfiguracaoEletricaDivergente");
    }

    [Fact]
    public void Installed_power_ignores_explicit_electrical_configuration()
    {
        var result = Calculator.Calculate(ReferenceInput(modulesPerString: 2, stringCount: 5));

        Assert.Equal(9, result.ModuleCount);
        Assert.Equal(9 * 700 / 1000.0, result.InstalledKwp, 6);
    }

    [Fact]
    public void Partial_config_only_modules_per_string_overallocating_is_blocking()
    {
        // 4 módulos/string para 9 módulos => 3 strings => 12 ≠ 9 (sobrealocação).
        var result = Calculator.Calculate(ReferenceInput(modulesPerString: 4));

        Assert.Contains(result.Alerts, a =>
            a.Code == "ConfiguracaoEletricaDivergente" && a.Severity == SolarSizingAlertSeverity.Blocking);
    }

    [Fact]
    public void Partial_config_only_modules_per_string_exact_is_not_blocking()
    {
        // 3 módulos/string para 9 módulos => 3 strings => 9 = 9.
        var result = Calculator.Calculate(ReferenceInput(modulesPerString: 3));

        Assert.DoesNotContain(result.Alerts, a => a.Code == "ConfiguracaoEletricaDivergente");
        Assert.Equal(9, (long)result.ModulesPerString * result.StringCount);
    }

    [Fact]
    public void Partial_config_only_string_count_overallocating_is_blocking()
    {
        // 2 strings para 9 módulos => 5 módulos/string => 10 ≠ 9 (sobrealocação).
        var result = Calculator.Calculate(ReferenceInput(stringCount: 2));

        Assert.Contains(result.Alerts, a =>
            a.Code == "ConfiguracaoEletricaDivergente" && a.Severity == SolarSizingAlertSeverity.Blocking);
    }

    [Fact]
    public void Partial_config_only_string_count_exact_is_not_blocking()
    {
        // 3 strings para 9 módulos => 3 módulos/string => 9 = 9.
        var result = Calculator.Calculate(ReferenceInput(stringCount: 3));

        Assert.DoesNotContain(result.Alerts, a => a.Code == "ConfiguracaoEletricaDivergente");
        Assert.Equal(9, (long)result.ModulesPerString * result.StringCount);
    }

    [Fact]
    public void Automatic_configuration_allocates_exactly_module_count()
    {
        var withoutInverter = Calculator.Calculate(ReferenceInput());
        Assert.Equal(withoutInverter.ModuleCount, (long)withoutInverter.ModulesPerString * withoutInverter.StringCount);
        Assert.DoesNotContain(withoutInverter.Alerts, a => a.Code == "ConfiguracaoEletricaDivergente");

        var withInverter = Calculator.Calculate(ReferenceInput(inverter: RefInverter()));
        Assert.Equal(withInverter.ModuleCount, (long)withInverter.ModulesPerString * withInverter.StringCount);
        Assert.DoesNotContain(withInverter.Alerts, a => a.Code == "ConfiguracaoEletricaDivergente");
    }

    [Fact]
    public void Automatic_configuration_respects_voltage_limit_and_keeps_exact_allocation()
    {
        // Limite de tensão: floor(165.2 / 41.3) = 4 módulos/string no MPPT.
        // O maior divisor de 9 que respeita o limite é 3 => 3 módulos/string × 3 strings = 9.
        var inverter = RefInverter(mpptVoltageMin: 0, mpptVoltageMax: 165.2, maxInputVoltage: 600);
        var result = Calculator.Calculate(ReferenceInput(inverter: inverter));

        Assert.Equal(9, (long)result.ModulesPerString * result.StringCount);
        Assert.InRange(result.ModulesPerString, 1, 4);
        Assert.DoesNotContain(result.Alerts, a => a.Code == "ConfiguracaoEletricaDivergente");
    }

    [Fact]
    public void Automatic_config_with_large_module_count_finds_largest_divisor_in_bounded_time()
    {
        // moduleCount grande (~2 bilhões) regride o loop proporcional a moduleCount no cálculo
        // do maior divisor <= limite de tensão. hsp=1000 e potência=1 tornam o caminho de
        // dimensionamento exato: requiredModules = consumption / 30.
        var hsp = new double[12];
        Array.Fill(hsp, 1000.0);
        var k = new double[12];
        Array.Fill(k, 1.0);
        var consumption = new double[12];
        Array.Fill(consumption, 60_000_000_000.0);

        var module = SolarModuleSpec.Create(powerWp: 1.0, voc: 1.0, vmp: 1.0, isc: 1.0, imp: 1.0, areaM2: 1.0);
        var inverter = SolarInverterSpec.Create(
            nominalPowerW: 2_000_000_000,
            mpptVoltageMin: 0,
            mpptVoltageMax: 1_000_000_000,
            maxInputVoltage: 1_000_000_000,
            maxInputCurrent: 1_000_000,
            mpptCount: 2,
            maxStringsPerMppt: 1);

        var input = SolarSizingInput.Create(
            consumption, hsp, k, module, SolarLosses.Create(0, 0, 0, 0, 0, 0, 0, 0, 0),
            "Norte", 10, availableAreaM2: 3_000_000_000.0, inverter: inverter);

        var result = Calculator.Calculate(input);

        Assert.Equal(2_000_000_000, result.ModuleCount);

        // Maior divisor de moduleCount <= limite de tensão (1e9), conferido por referência
        // independente; a alocação automática deve reproduzi-lo e permanecer exata.
        var expected = LargestDivisorAtMostReference(result.ModuleCount, 1_000_000_000);
        Assert.Equal(expected, result.ModulesPerString);
        Assert.Equal(result.ModuleCount, (long)result.ModulesPerString * result.StringCount);
    }

    [Fact]
    public void Total_losses_at_100_percent_is_blocking()
    {
        var allLoss = SolarLosses.Create(1.0, 0, 0, 0, 0, 0, 0, 0, 0);
        var result = Calculator.Calculate(WithLosses(allLoss));

        Assert.True(result.HasBlockingAlert);
        Assert.Contains(result.Alerts, a => a.Code == "PerdasTotaisAcimaDe100");
        Assert.Equal(0.0, result.AnnualGenerationKWh);
        Assert.Equal(0.0, result.AnnualEnergyCompensableKWh);
    }

    [Fact]
    public void Total_losses_above_100_percent_yields_zero_generation_and_no_negatives()
    {
        var losses = SolarLosses.Create(0.6, 0.6, 0, 0, 0, 0, 0, 0, 0); // total = 1.2
        var result = Calculator.Calculate(WithLosses(losses));

        Assert.True(result.HasBlockingAlert);
        Assert.Contains(result.Alerts, a => a.Code == "PerdasTotaisAcimaDe100" && a.Severity == SolarSizingAlertSeverity.Blocking);

        Assert.Equal(0.0, result.AnnualGenerationKWh);
        Assert.Equal(0.0, result.AnnualEnergyCompensableKWh);
        Assert.All(result.MonthlyGenerationKWh, g => Assert.Equal(0.0, g));
        Assert.All(result.MonthlyEnergyCompensableKWh, c => Assert.Equal(0.0, c));
    }

    [Fact]
    public void Inverter_ratio_out_of_range_is_non_blocking_warning()
    {
        var inverter = RefInverter(nominalPowerW: 12000);
        var result = Calculator.Calculate(ReferenceInput(inverter: inverter));

        Assert.False(result.HasBlockingAlert);
        Assert.Contains(result.Alerts, a => a.Code == "FatorDimensionamentoGlobalForaDaFaixa" && a.Severity == SolarSizingAlertSeverity.Warning);
    }

    // --- Determinismo ---

    [Fact]
    public void Calculator_is_deterministic()
    {
        var first = Calculator.Calculate(ReferenceInput());
        var second = Calculator.Calculate(ReferenceInput());

        Assert.Equal(first.AnnualGenerationKWh, second.AnnualGenerationKWh);
        Assert.Equal(first.AverageGenerationKWh, second.AverageGenerationKWh);
        Assert.Equal(first.ModuleCount, second.ModuleCount);
        Assert.Equal(first.MonthlyGenerationKWh, second.MonthlyGenerationKWh);
    }

    // --- Helpers ---

    private static SolarSizingInput WithLosses(SolarLosses losses) =>
        SolarSizingInput.Create(RefConsumption, RefHsp, RefK, RefModule(), losses, "Norte", 10, 50.0);

    private static int LargestDivisorAtMostReference(int n, int max)
    {
        if (max >= n)
            return n;
        if (max < 1)
            return 1;

        var best = 1;
        for (var i = 1; (long)i * i <= n; i++)
        {
            if (n % i != 0)
                continue;

            if (i <= max)
                best = Math.Max(best, i);

            var coDivisor = n / i;
            if (coDivisor <= max)
                best = Math.Max(best, coDivisor);
        }

        return best;
    }
}
