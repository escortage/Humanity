using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client.Humanity.Visuals;

public sealed class BattleCraterOverlay(IEntityManager entities) : GridOverlay
{
    private readonly SharedTransformSystem _transforms = entities.System<SharedTransformSystem>();
    private readonly BattleCraterSystem _craters = entities.System<BattleCraterSystem>();

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        handle.SetTransform(_transforms.GetWorldMatrix(Grid.Owner));
        _craters.DrawGround(Grid.Owner, handle, args.WorldAABB);
        handle.SetTransform(Matrix3x2.Identity);
    }
}
