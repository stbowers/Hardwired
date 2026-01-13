#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using Assets.Scripts;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.UI;
using Assets.Scripts.Util;
using Cysharp.Threading.Tasks.Triggers;
using Hardwired.Networks;
using Hardwired.Objects.Electrical;
using Hardwired.Utility;
using HarmonyLib;
using UnityEngine;

namespace Hardwired.Objects
{
    /// <summary>
    /// Base class for Hardwired devices. Derrives from Device so that it can be added to CableNetwork.DeviceList, but modifies the behavior of the standard Device class by:
    /// 1) Merges all connected networks together, so devices can have multiple connections to the same circuit (for example, transformer with primary/secondary side, or inline series components).
    /// 2) Allows devices to be placed next to each other
    /// </summary>
    public class ElectricalStructure : Device
    {
        // private Func<CanConstructInfo> _smallGrid_CanConstruct;

        // public CableNetwork? CableNetwork { get; protected set; }

        // public ElectricalStructure()
        // {
        //     _smallGrid_CanConstruct = ReflectionHelper.GetNonVirtualDelegate<Func<CanConstructInfo>>(typeof(SmallGrid), nameof(SmallGrid.CanConstruct), this);
        // }

        // // public override void OnRegistered(Cell cell)
        // // {
        // //     base.OnRegistered(cell);
        // //     MergeConnections();
        // // }

        // // public override void OnDeregistered()
        // // {
        // //     base.OnDeregistered();
        // //     MergeConnections();
        // // }

        // // public override void OnGridPlaced(SmallGrid newOccupant)
        // // {
        // //     base.OnGridPlaced(newOccupant);
        // //     MergeConnections();
        // // }

        // // public override void OnGridRemoved(SmallGrid oldOccupant)
        // // {
        // //     base.OnGridRemoved(oldOccupant);
        // //     MergeConnections();
        // // }

        // // public override void OnGridAdjacentPlaced(SmallGrid neighbor)
        // // {
        // //     base.OnGridAdjacentPlaced(neighbor);
        // //     MergeConnections();
        // // }

        // // public override void OnGridAdjacentRemoved(SmallGrid neighbor)
        // // {
        // //     base.OnGridAdjacentRemoved(neighbor);
        // //     MergeConnections();
        // // }

        // private void MergeCircuits(CableNetwork a, CableNetwork b)
        // {


        // }

        // public override void OnAddCableNetwork(CableNetwork newNetwork)
        // {
        //     base.OnAddCableNetwork(newNetwork);

        //     Hardwired.LogDebug($"ElectricalStructure.OnAddCableNetwork() -- {PrefabName}, {newNetwork.ReferenceId}");

        //     foreach (var cable in PowerCables)
        //     {
        //         if (cable.CableNetwork != newNetwork)
        //         {
        //             MergeCircuits(cable.CableNetwork, newNetwork);
        //         }
        //     }

        //     // CableNetwork.Event onNetworkChanged = null!;
        //     // onNetworkChanged = () =>
        //     // {
        //     //     CableNetwork.OnNetworkChanged -= onNetworkChanged;

        //     //     foreach (var cable in PowerCables)
        //     //     {
        //     //         if (cable.CableNetwork != newNetwork)
        //     //         {
        //     //             newNetwork.Add(cable);
        //     //         }
        //     //     }
        //     // };
        //     // CableNetwork.OnNetworkChanged += onNetworkChanged;


        //     CableNetwork = newNetwork;
        //     _needUpdate = true;


        //     // Action onNetworkChanged = null!;
        //     // onNetworkChanged = () =>
        //     // {
        //     //     Hardwired.LogDebug($"inside OnNetworkChanged");
        //     //     _onServerTick -= onNetworkChanged;

        //     //     foreach (var cable in PowerCables)
        //     //     {
        //     //         if (cable.CableNetwork != newNetwork)
        //     //         {
        //     //             newNetwork.Add(cable);
        //     //             newNetwork.DirtyPowerAndDataDeviceLists();
        //     //         }
        //     //     }
        //     // };
        //     // _onServerTick += onNetworkChanged;
        // }

        // public override void OnRemoveCableNetwork(CableNetwork oldNetwork)
        // {
        //     base.OnRemoveCableNetwork(oldNetwork);

        //     Hardwired.LogDebug($"ElectricalStructure.OnRemoveCableNetwork() -- {PrefabName}, {oldNetwork.ReferenceId}");

        //     // TOOD: check if any cable networks should be removed from the circuit

        //     // _needUpdate = true;

        //     // Action onNetworkChanged = null!;
        //     // onNetworkChanged = () =>
        //     // {
        //     //     _onServerTick -= onNetworkChanged;
        //     //     foreach (var cable in PowerCables)
        //     //     {
        //     //         if (cable.CableNetwork == oldNetwork)
        //     //         {
        //     //             oldNetwork.Remove(cable);
        //     //         }
        //     //     }
        //     // };
        //     // _onServerTick += onNetworkChanged;

        // }

        // // Action? _onServerTick;
        // bool _needUpdate;

        // public override void OnServerTick(float deltaTime)
        // {
        //     base.OnServerTick(deltaTime);

        //     if (_needUpdate)
        //     {
        //         MergeConnections();
        //         _needUpdate = false;
        //     }

        //     // _onServerTick?.Invoke();
        // }

        // public override CanConstructInfo CanConstruct()
        // {
        //     // TODO - I'd like to eventually override the behavior of Device so that multiple components can be connected directly to each other
        //     // (i.e. without a cable between). The below code works, but the problem is CableNetwork does not support this by default, so it would
        //     // likely be required to patch it.
        //     return base.CanConstruct();

        //     // Call SmallGrid.CanConstruct(), skipping Device.CanConstruct()...
        //     // The Device version of this method prohibits placing any device adjacent to another device, but our components can handle that just fine
        //     // return _smallGrid_CanConstruct();
        // }

        // /// <summary>
        // /// (re)initializes CableNetwork by merging together all networks from connected devices and/or cables
        // /// </summary>
        // protected void MergeConnections()
        // {
        //     Hardwired.LogDebug($"ElectricalStructure.MergeConnections() == {PrefabName} == {CableNetwork?.ReferenceId}");

        //     foreach (var cable in ConnectedCables())
        //     {
        //         if (CableNetwork is null)
        //         {
        //             CableNetwork = cable.CableNetwork;
        //         }
        //         else if (CableNetwork != cable.CableNetwork)
        //         {
        //             CableNetwork.Add(cable);
        //         }
        //     }
        //     // List<Cable> cables = ConnectedCables();


        //     // // Get a list of all connected cable networks
        //     // List<CableNetwork> cableNetworks =
        //     //     // devices.Select(d => (d as ElectricalStructure)?.CableNetwork ?? d.PowerCableNetwork)
        //     //     cables.Select(c => c.CableNetwork)
        //     //     .Where(network => network != null)
        //     //     .Distinct().ToList();
        //     
        //     // // Get new network to join by merging all connected networks together
        //     // CableNetwork = CableNetwork.Merge(cableNetworks);
        //     // CableNetwork?.AddDevice(this);
        // }
    }
}
