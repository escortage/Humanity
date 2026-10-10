using System.Linq;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Humanity.Combat;

public static class GameplayAimInput
{
    public static bool CanAim(IUserInterfaceManager ui) => ui.KeyboardFocused == null
        && ui.CurrentlyHovered is IViewportControl && !ui.ModalRoot.Children.Any(control => control.VisibleInTree);
}
