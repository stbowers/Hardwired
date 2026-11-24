#nullable enable

using Assets.Scripts.Objects;
using LibConstruct;

namespace Hardwired.Prefabs.JunctionBox
{
    public class JunctionBoxBoard : PlacementBoard
    {
        public override float GridSize => 0.5f / 2;

        public override IPlacementBoardStructure? EquivalentStructure(Structure? structure)
        {
            return structure?.name switch
            {
                _ => structure as IPlacementBoardStructure,
            };
        }
    }
}