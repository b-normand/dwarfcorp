using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using BloomPostprocess;
using DwarfCorp.Gui;
using DwarfCorp.Gui.Widgets;
using DwarfCorp.Tutorial;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using DwarfCorp.GameStates;
using Newtonsoft.Json;
using DwarfCorp.Events;
using System.Diagnostics;
using System.Text;

namespace DwarfCorp
{
    public partial class PersistentWorldData
    {
        public List<Zone> Zones = new List<Zone>();
        public List<BuildZoneOrder> BuildDesignations = new List<BuildZoneOrder>();
        public int NextRoomID = 0;
    }

    public partial class WorldManager
    {
        public IEnumerable<Zone> EnumerateZones()
        {
            foreach (var room in PersistentData.Zones)
                yield return room;
            yield break;
        }

        public int ComputeRemainingStockpileSpace()
        {
            return EnumerateZones().OfType<Stockpile>().Where(pile => !(pile is Graveyard)).Sum(pile => pile.ResourceCapacity - pile.Resources.TotalCount);
        }

        public int ComputeTotalStockpileSpace()
        {
            return EnumerateZones().OfType<Stockpile>().Where(pile => !(pile is Graveyard)).Sum(pile => pile.ResourceCapacity);
        }

        public Zone GetNearestRoomOfType(string typeName, Vector3 position)
        {
            Zone desiredRoom = null;
            float nearestDistance = float.MaxValue;

            foreach (var room in EnumerateZones())
            {
                if (room.Type.Name != typeName || !room.IsBuilt) continue;
                float dist =
                    (room.GetNearestVoxel(position).WorldPosition - position).LengthSquared();

                if (dist < nearestDistance)
                {
                    nearestDistance = dist;
                    desiredRoom = room;
                }
            }

            return desiredRoom;
        }

        public MaybeNull<Stockpile> GetStockpileContainingResource(Resource Resource)
        {
            foreach (var stockpile in EnumerateZones().OfType<Stockpile>())
                if (stockpile.Resources.Contains(Resource))
                    return stockpile;
            return null;
        }

        public IEnumerable<KeyValuePair<Stockpile, Resource>> GetStockpilesContainingResources(Vector3 biasPos, IEnumerable<ResourceTypeAmount> required)
        {
            foreach (var amount in required)
            {
                var numGot = 0;
                foreach (var stockpile in EnumerateZones().OfType<Stockpile>().OrderBy(s => (s.GetBoundingBox().Center() - biasPos).LengthSquared()))
                {
                    if (numGot >= amount.Count)
                        break;

                    foreach (var resource in stockpile.Resources.Enumerate().Where(sResource => sResource.TypeName == amount.Type))
                    {
                        numGot += 1;
                        yield return new KeyValuePair<Stockpile, Resource>(stockpile, resource);
                    }
                }
            }
        }

        public IEnumerable<KeyValuePair<Stockpile, Resource>> GetStockpilesContainingResources( IEnumerable<ResourceTypeAmount> required)
        {
            foreach (var amount in required)
            {
                var numGot = 0;
                foreach (var stockpile in EnumerateZones().OfType<Stockpile>())
                {
                    if (numGot >= amount.Count)
                        break;

                    foreach (var resource in stockpile.Resources.Enumerate().Where(sResource => sResource.TypeName == amount.Type))
                    {
                        numGot += 1;
                        yield return new KeyValuePair<Stockpile, Resource>(stockpile, resource);
                        if (numGot >= amount.Count)
                            break;
                    }
                }
            }
        }

        public IEnumerable<KeyValuePair<Stockpile, Resource>> GetStockpilesContainingResources(List<ResourceTagAmount> tags)
        {
            foreach (var tag in tags)
            {
                int numGot = 0;
                foreach (var stockpile in EnumerateZones().OfType<Stockpile>())
                {
                    if (numGot >= tag.Count)
                        break;

                    foreach (var resource in stockpile.Resources.Enumerate().Where(sResource => Library.GetResourceType(sResource.TypeName).HasValue(out var res) && res.Tags.Contains(tag.Tag)))
                    {
                        numGot += 1;
                        yield return new KeyValuePair<Stockpile, Resource>(stockpile, resource);

                        if (numGot >= tag.Count)
                            break;
                    }
                }
            }
        }

        public KeyValuePair<Stockpile, Resource>? GetFirstStockpileContainingResourceWithMatchingTag(List<String> Tags)
        {
            foreach (var stockpile in EnumerateZones().OfType<Stockpile>())
            {
                foreach (var resource in stockpile.Resources.Enumerate().Where(sResource => Library.GetResourceType(sResource.TypeName).HasValue(out var res) && res.Tags.Any(t => Tags.Contains(t))))
                    return new KeyValuePair<Stockpile, Resource>(stockpile, resource);
            }

            return null;
        }

        public Zone FindNearestZone(Vector3 position)
        {
            Zone desiredRoom = null;
            float nearestDistance = float.MaxValue;

            foreach (var room in EnumerateZones())
            {
                if (room.Voxels.Count == 0) continue;
                float dist =
                    (room.GetNearestVoxel(position).WorldPosition - position).LengthSquared();

                if (dist < nearestDistance)
                {
                    nearestDistance = dist;
                    desiredRoom = room;
                }
            }


            return desiredRoom;
        }

        public bool HasFreeStockpile()
        {
            return EnumerateZones().OfType<Stockpile>().Any(s => !(s is Graveyard) && s.IsBuilt && !s.IsFull());
        }

        public bool HasFreeStockpile(ResourceTypeAmount toPut)
        {
            return EnumerateZones().OfType<Stockpile>().Any(s => s.IsBuilt && !s.IsFull() && s.IsAllowed(toPut.Type));
        }

        public bool IsActiveBuildZoneOrder(BuildZoneOrder buildRooom)
        {
            return PersistentData.BuildDesignations.Contains(buildRooom);
        }

        public void DestroyBuildDesignation(VoxelHandle V)
        {
            var vox = GetBuildDesignation(V);
            if (vox != null && vox.Order != null)
            {
                vox.Order.Destroy();
                if (vox.Order.DisplayWidget != null)
                    UserInterface.Gui.DestroyWidget(vox.Order.DisplayWidget);
                PersistentData.BuildDesignations.Remove(vox.Order);
                PersistentData.Zones.Remove(vox.Order.ToBuild);
            }
        }

        public void DestroyZone(Zone Z)
        {
            PersistentData.Zones.Remove(Z);

            var existingDesignations = GetDesignationsAssociatedWithZone(Z);
            BuildZoneOrder buildRoomDes = null;
            foreach (var des in existingDesignations)
            {
                des.Order.VoxelOrders.Remove(des);
                buildRoomDes = des.Order;
            }

            if (buildRoomDes != null && buildRoomDes.DisplayWidget != null)
                UserInterface.Gui.DestroyWidget(buildRoomDes.DisplayWidget);

            PersistentData.BuildDesignations.Remove(buildRoomDes);
            Z.Destroy();
        }

        public void AddZone(Zone Z)
        {
            PersistentData.Zones.Add(Z);
        }

        public Zone FindZone(String ID)
        {
            return PersistentData.Zones.FirstOrDefault(s => s.ID == ID);
        }

        public bool IsInZone(VoxelHandle v)
        {
            return PersistentData.Zones.Any(r => r.ContainsVoxel(v));
        }

        public Zone GetZoneThatContainsVoxel(VoxelHandle V)
        {
            return PersistentData.Zones.FirstOrDefault(r => r.ContainsVoxel(V));
        }

        public bool IsBuildDesignation(VoxelHandle v)
        {
            return PersistentData.BuildDesignations.SelectMany(room => room.VoxelOrders).Any(buildDesignation => buildDesignation.Voxel == v);
        }

        public BuildVoxelOrder GetBuildDesignation(VoxelHandle v)
        {
            return PersistentData.BuildDesignations.SelectMany(room => room.VoxelOrders).FirstOrDefault(buildDesignation => buildDesignation.Voxel == v);
        }

        public Zone GetMostLikelyZone(VoxelHandle v)
        {
            foreach (var r in PersistentData.Zones.Where(r => r.ContainsVoxel(v)))
                return r;

            BoundingBox larger = new BoundingBox(v.GetBoundingBox().Min - new Vector3(0.5f, 0.5f, 0.5f), v.GetBoundingBox().Max + new Vector3(0.5f, 0.5f, 0.5f));

            return (from room in PersistentData.BuildDesignations
                    from buildDesignation in room.VoxelOrders
                    where larger.Intersects(buildDesignation.Voxel.GetBoundingBox())
                    select buildDesignation.ToBuild).FirstOrDefault();
        }

        public List<BuildVoxelOrder> GetDesignationsAssociatedWithZone(Zone room)
        {
            return (from roomDesignation in PersistentData.BuildDesignations
                    from des in roomDesignation.VoxelOrders
                    where des.ToBuild == room
                    select des).ToList();
        }

        public void UpdateZones(DwarfTime Time)
        {
            var toRemove = PersistentData.BuildDesignations.Where(build => build.IsBuilt).ToList();

            foreach (var build in toRemove)
            {
                if (build.DisplayWidget != null)
                    UserInterface.Gui.DestroyWidget(build.DisplayWidget);
                PersistentData.BuildDesignations.Remove(build);
            }

            foreach (var zone in PersistentData.Zones)
                zone.Update(Time);

            foreach (var buildOrder in PersistentData.BuildDesignations)
            {
                if (buildOrder.IsBuilt)
                {
                    if (buildOrder.DisplayWidget != null)
                    {
                        buildOrder.DisplayWidget.Root.DestroyWidget(buildOrder.DisplayWidget);
                        buildOrder.DisplayWidget = null;
                    }
                }
                else
                {
                    var requiredResources = buildOrder.ListRequiredResources();
                    if (buildOrder.DisplayWidget == null)
                    {
                        if (!HasResourcesWithTags(requiredResources))
                        {
                            var resourceList = new StringBuilder();
                            foreach (var resource in requiredResources)
                            {
                                resourceList.Append(resource.Count);
                                resourceList.Append(" ");
                                resourceList.Append(resource.Tag);
                            }

                            buildOrder.DisplayWidget = UserInterface.Gui.RootItem.AddChild(new Gui.Widget()
                            {
                                Border = "border-dark",
                                TextColor = Color.White.ToVector4(),
                                Text = String.Format("Need {0} to build this {1}", resourceList, buildOrder.ToBuild.Type.Name),
                                Rect = new Rectangle(0, 0, 200, 40),
                                Font = "font8",
                                TextVerticalAlign = Gui.VerticalAlign.Center,
                                TextHorizontalAlign = Gui.HorizontalAlign.Center,
                                OnClick = (sender, args) =>
                                {
                                    sender.Hidden = true;
                                }
                            });

                            UserInterface.Gui.RootItem.SendToBack(buildOrder.DisplayWidget);
                        }
                    }
                    else
                    {
                        if (HasResourcesWithTags(requiredResources))
                        {
                            buildOrder.DisplayWidget.Root.DestroyWidget(buildOrder.DisplayWidget);
                            buildOrder.DisplayWidget = null;
                        }
                        else
                        {
                            var center = buildOrder.GetBoundingBox().Center();
                            var projection = Renderer.Camera.Project(center);
                            if (projection.Z < 0.9999)
                            {
                                buildOrder.DisplayWidget.Rect = new Rectangle((int)(projection.X - buildOrder.DisplayWidget.Rect.Width / 2),
                                    (int)(projection.Y - buildOrder.DisplayWidget.Rect.Height / 2),
                                    buildOrder.DisplayWidget.Rect.Width, buildOrder.DisplayWidget.Rect.Height);
                                buildOrder.DisplayWidget.Invalidate();
                            }
                        }
                    }
                }
            }
        }
    }
}
