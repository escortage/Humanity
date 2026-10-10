using Content.Server.Administration;
using Content.Server.GameTicking.Rules;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Humanity.Commands;

[AdminCommand(AdminFlags.Round)]
public sealed partial class StartBattleCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;

    public string Command => "startbattle";
    public string Description => "Начинает бой.";
    public string Help => "startbattle - Начать бой.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Help);
            return;
        }
        if (!_entities.System<GracewallRuleSystem>().TryStartBattle())
        {
            shell.WriteError("Вы в лобби.");
            return;
        }
        shell.WriteLine("Бой начался.");
    }
}
