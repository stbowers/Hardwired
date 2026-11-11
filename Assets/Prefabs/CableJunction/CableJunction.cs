#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Motherboards;
using Hardwired.Patches;
using Hardwired.Utility.Extensions;
using LaunchPadBooster;
using LaunchPadBooster.Utils;
using UnityEngine;

namespace Hardwired.Prefabs.CableJunction
{
    public class CableJunction : Cable, IPatchOnLoad
    {

        public Material? ColorRed;
        public Material? ColorGray;
        public Material? ColorBlue;

        public void PatchOnLoad()
        {
            this.ReplaceMaterialWithColor(ColorRed, ColorType.Red);
            this.ReplaceMaterialWithColor(ColorGray, ColorType.Gray);
            this.ReplaceMaterialWithColor(ColorBlue, ColorType.Blue);
            this.SetEntryTool(PrefabNames.CableCoil);
            this.SetExitTool(PrefabNames.WireCutters);
            this.AddToMultiConstructor(PrefabNames.CableCoil);
        }
    }
}
