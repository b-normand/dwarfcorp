using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;

namespace DwarfCorp
{
    public static class GrassLibrary
    {
        private static Dictionary<string, GrassType> Types = new Dictionary<string, GrassType>();
        private static List<GrassType> TypeList;
        private static bool Initialized = false;

        public static IEnumerable<GrassType> EnumerateTypes()
        {
            InitializeLibrary();
            return TypeList;
        }

        private static GrassType.FringeTileUV[] CreateFringeUVs(Point[] Tiles)
        {
            global::System.Diagnostics.Debug.Assert(Tiles.Length == 3);

            var r = new GrassType.FringeTileUV[8];

            // North
            r[0] = new GrassType.FringeTileUV(Tiles[0].X, (Tiles[0].Y * 2) + 1, 16, 32);
            // East
            r[1] = new GrassType.FringeTileUV((Tiles[1].X * 2) + 1, Tiles[1].Y, 32, 16);
            // South
            r[2] = new GrassType.FringeTileUV(Tiles[0].X, (Tiles[0].Y * 2), 16, 32);
            // West
            r[3] = new GrassType.FringeTileUV(Tiles[1].X * 2, Tiles[1].Y, 32, 16);

            // NW
            r[4] = new GrassType.FringeTileUV((Tiles[2].X * 2) + 1, (Tiles[2].Y * 2) + 1, 32, 32);
            // NE
            r[5] = new GrassType.FringeTileUV((Tiles[2].X * 2), (Tiles[2].Y * 2) + 1, 32, 32);
            // SE
            r[6] = new GrassType.FringeTileUV((Tiles[2].X * 2), (Tiles[2].Y * 2), 32, 32);
            // SW
            r[7] = new GrassType.FringeTileUV((Tiles[2].X * 2) + 1, (Tiles[2].Y * 2), 32, 32);

            return r;
        }

        private static void InitializeLibrary()
        {
            if (Initialized) return;
            Initialized = true;

            TypeList = FileUtils.LoadJsonListFromDirectory<GrassType>(ContentPaths.grass_types, null, g => g.Name);

            byte ID = 1;
            foreach (var type in TypeList)
            {
                if (type.Name == "_empty")
                {
                    type.ID = 0;
                    continue;
                }
                else
                {
                    type.ID = ID;
                    ++ID;
                }

                Types[type.Name] = type;

                if (type.FringeTiles != null)
                    type.FringeTransitionUVs = CreateFringeUVs(type.FringeTiles);

                if (type.InitialDecayValue > VoxelConstants.MaximumGrassDecay)
                {
                    type.InitialDecayValue = VoxelConstants.MaximumGrassDecay;
                    Console.WriteLine("Grass type " + type.Name + " with invalid InitialDecayValue");
                }
            }

            if (ID > VoxelConstants.MaximumGrassTypes)
                Console.WriteLine("Allowed number of grass types exceeded. Limit is " + VoxelConstants.MaximumGrassTypes);

            TypeList = TypeList.OrderBy(v => v.ID).ToList();

            Console.WriteLine("Loaded Grass Library.");
        }

        public static GrassType GetGrassType(byte id)
        {
            InitializeLibrary();
            return TypeList[id];
        }

        public static GrassType GetGrassType(string name)
        {
            InitializeLibrary();
            if (name == null)
            {
                return null;
            }
            GrassType r = null;
            Types.TryGetValue(name, out r);
            return r;
        }

        public static Dictionary<int, String> GetGrassTypeMap()
        {
            InitializeLibrary();
            var r = new Dictionary<int, String>();
            for (var i = 0; i < TypeList.Count; ++i)
                r.Add(i, TypeList[i].Name);
            return r;
        }
    }
}