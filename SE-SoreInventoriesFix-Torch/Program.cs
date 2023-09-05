using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Torch;
using Torch.API;
using Torch.Managers.PatchManager;
using VRage.Game.ModAPI;
using VRage.Game;
using VRage.Network;
using Sandbox.Engine.Multiplayer;

namespace SE_StoreInventoriesFix_Torch
{
    public class Main : TorchPluginBase
    {
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
        }
    }

    [PatchShim]
    public class StoreBlockPatch
    {
        private static readonly MethodInfo getConnectedGridInventories =
            typeof(MyStoreBlock).GetMethod("GetConnectedGridInventories", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception(nameof(getConnectedGridInventories) + " Not Found!");

        private static readonly MethodInfo getConnectedGridInventoriesPatch =
            typeof(StoreBlockPatch).GetMethod("GetConnectedGridInventoriesPatch", BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception(nameof(getConnectedGridInventoriesPatch) + " Not Found!");

        private static readonly MethodInfo HasAccessMethod =
            typeof(MyStoreBlock).GetMethod("HasAccess", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo OnGetConnectedGridInventoriesResultMethod =
            typeof(MyStoreBlock).GetMethod("OnGetConnectedGridInventoriesResult", BindingFlags.Instance | BindingFlags.NonPublic);

        public static bool GetConnectedGridInventoriesPatch(MyStoreBlock __instance)
        {
            if (!HasAccess(__instance))
                return false;

            long identityId = MySession.Static.Players.TryGetIdentityId(MyEventContext.Current.Sender.Value);
            List<long> arg = new List<long>();
            foreach (MySlimBlock block in __instance.CubeGrid.GetBlocks())
            {
                if (block.FatBlock is MyShipConnector fatBlock && fatBlock != null && fatBlock.Connected && (bool)fatBlock.TradingEnabled && ((Sandbox.ModAPI.Ingame.IMyShipConnector)fatBlock).Status == Sandbox.ModAPI.Ingame.MyShipConnectorStatus.Connected)
                {
                    foreach (long invEntityId in GetInventoryEntities(fatBlock, identityId))
                    {
                        if (!arg.Contains(invEntityId))
                        {
                            arg.Add(invEntityId);
                        }
                    }
                }
            }

            Action<List<long>> del = OnGetConnectedGridInventoriesResultMethod.CreateDelegate<Action<List<long>>>(__instance);

            MyMultiplayer.RaiseEvent(
                __instance,
                (MyStoreBlock x) => del,
                arg,
                MyEventContext.Current.Sender);
            return false;
        }

        public static void Patch(PatchContext context)
        {
            context.GetPattern(getConnectedGridInventories).Prefixes.Add(getConnectedGridInventoriesPatch);
        }

        private static bool HasAccess(MyStoreBlock storeBlock)
        {
            return (bool)HasAccessMethod.Invoke(storeBlock, null);
        }

        private static List<long> GetInventoryEntities(MyShipConnector connector, long identityId)
        {
            List<long> inventoryEntities = new List<long>();
            if (connector.Other == null)
                return inventoryEntities;
            List<MyCubeGrid> groupNodes = MyCubeGridGroups.Static.GetGroups(GridLinkTypeEnum.Mechanical).GetGroupNodes(connector.Other.CubeGrid);
            if (groupNodes == null || groupNodes.Count == 0 || connector.Other == null)
                return inventoryEntities;
            foreach (MyCubeGrid myCubeGrid in groupNodes)
            {
                if (myCubeGrid.BigOwners.Contains(identityId))
                {
                    foreach (MySlimBlock cubeBlock in myCubeGrid.CubeBlocks)
                    {
                        if (cubeBlock.FatBlock != null)
                        {
                            if (cubeBlock.FatBlock is MyCargoContainer fatBlock1)
                            {
                                if (fatBlock1.HasPlayerAccess(identityId, MyRelationsBetweenPlayerAndBlock.NoOwnership))
                                {
                                    if (fatBlock1.InventoryCount != 0)
                                        inventoryEntities.Add(fatBlock1.EntityId);
                                }
                                else
                                    continue;
                            }
                            if (cubeBlock.FatBlock is MyGasTank fatBlock2 && fatBlock2.HasPlayerAccess(identityId, MyRelationsBetweenPlayerAndBlock.NoOwnership))
                                inventoryEntities.Add(fatBlock2.EntityId);
                        }
                    }
                }
            }
            return inventoryEntities;
        }
    }
}
