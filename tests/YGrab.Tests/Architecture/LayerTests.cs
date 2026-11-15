// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using YGrab.Engine;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchModel = ArchUnitNET.Domain.Architecture;

namespace YGrab.Tests.Architecture;

// eng/BannedSymbols.<Layer>.txt enforces the namespaces, these rules cover the assemblies and what an analyzer cannot see
public sealed class LayerTests
{
    private const string DeezerNamespace = "YGrab.Infrastructure.Deezer";

    private static readonly Assembly _cli = Assembly.Load("ygrab");
    private static readonly Assembly _contracts = Assembly.Load("YGrab.Contracts");
    private static readonly Assembly _domain = Assembly.Load("YGrab.Domain");
    private static readonly Assembly _engine = Assembly.Load("YGrab.Engine");
    private static readonly Assembly _infrastructure = Assembly.Load("YGrab.Infrastructure");
    private static readonly Assembly _python = Assembly.Load("YGrab.Infrastructure.Python");
    private static readonly Assembly _host = Assembly.Load("YGrab.Host");

    private static readonly ArchModel _model = new ArchLoader()
        .LoadAssemblies(_cli, _contracts, _domain, _engine, _infrastructure, _python, _host)
        .Build();

    [Fact]
    public void Domain_depends_on_no_other_project() =>
        AssemblyTypes(_domain)
            .Should()
            .NotDependOnAny(
                AssemblyTypes(_cli, _contracts, _engine, _infrastructure, _python, _host)
            )
            .Check(_model);

    [Fact]
    public void Engine_depends_only_on_the_domain() =>
        AssemblyTypes(_engine)
            .Should()
            .NotDependOnAny(AssemblyTypes(_cli, _contracts, _infrastructure, _python, _host))
            .Check(_model);

    [Fact]
    public void Infrastructure_depends_on_neither_the_python_adapter_nor_the_clients() =>
        AssemblyTypes(_infrastructure)
            .Should()
            .NotDependOnAny(AssemblyTypes(_cli, _contracts, _python, _host))
            .Check(_model);

    [Fact]
    public void Python_adapter_depends_on_neither_the_infrastructure_nor_the_clients() =>
        AssemblyTypes(_python)
            .Should()
            .NotDependOnAny(AssemblyTypes(_cli, _contracts, _infrastructure, _host))
            .Check(_model);

    [Fact]
    public void Contracts_depend_on_no_other_project() =>
        AssemblyTypes(_contracts)
            .Should()
            .NotDependOnAny(AssemblyTypes(_cli, _domain, _engine, _infrastructure, _python, _host))
            .Check(_model);

    [Fact]
    public void Cli_depends_only_on_the_contracts() =>
        AssemblyTypes(_cli)
            .Should()
            .NotDependOnAny(AssemblyTypes(_domain, _engine, _infrastructure, _python, _host))
            .Check(_model);

    [Fact]
    public void Host_only_registers_the_infrastructure() =>
        AssemblyTypes(_host)
            .Should()
            .NotDependOnAny(
                Types()
                    .That()
                    .ResideInAssembly(_infrastructure)
                    .Or()
                    .ResideInAssembly(_python)
                    .And()
                    .DoNotHaveNameEndingWith("Extensions")
            )
            .Check(_model);

    [Fact]
    public void Nothing_outside_the_infrastructure_uses_the_deezer_namespace() =>
        Types()
            .That()
            .DoNotResideInAssembly(_infrastructure)
            .Should()
            .NotDependOnAny(Types().That().ResideInNamespace(DeezerNamespace))
            .Check(_model);

    [Fact]
    public void Engine_handlers_implement_a_handler_interface() =>
        Classes()
            .That()
            .ResideInAssembly(_engine)
            .And()
            .HaveNameEndingWith("Handler")
            .Should()
            .ImplementInterface(typeof(ICommandHandler<,>))
            .OrShould()
            .ImplementInterface(typeof(IQueryHandler<,>))
            .Check(_model);

    [Fact]
    public void Infrastructure_and_host_use_handler_interfaces_not_handler_classes() =>
        Types()
            .That()
            .ResideInAssembly(_infrastructure)
            .Or()
            .ResideInAssembly(_python)
            .Or()
            .ResideInAssembly(_host)
            .Should()
            .NotDependOnAny(
                Classes().That().ResideInAssembly(_engine).And().HaveNameEndingWith("Handler")
            )
            .Check(_model);

    private static GivenTypesConjunction AssemblyTypes(params Assembly[] assemblies)
    {
        var types = Types().That().ResideInAssembly(assemblies[0]);
        foreach (var assembly in assemblies.Skip(1))
        {
            types = types.Or().ResideInAssembly(assembly);
        }

        return types;
    }
}
