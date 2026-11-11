#nullable enable

using System;
using System.Linq;
using Assets.Scripts.Objects;
using HarmonyLib;

namespace Hardwired.Patches
{
    public interface IPatchOnLoad
    {
        public void PatchOnLoad();
    }
}