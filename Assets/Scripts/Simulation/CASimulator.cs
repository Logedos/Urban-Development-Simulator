using System;
using UnityEngine;

namespace Simulation
{
    public class CASimulator : MonoBehaviour
    {
        public float globalGrowthRate = 1.0f;

        [SerializeField] private CA_Model activeModel;

        private GridManager gridManager;
        private Cell[,] current;
        private Cell[,] next;
        private int width;
        private int height;
        private bool initialized;

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            gridManager = GridManager.Instance;
            if (gridManager == null)
            {
                initialized = false;
                return;
            }

            current = gridManager.GetGridReference();
            if (current == null)
            {
                initialized = false;
                return;
            }

            width = gridManager.width;
            height = gridManager.height;

            if (next == null || next.GetLength(0) != width || next.GetLength(1) != height)
            {
                next = new Cell[width, height];
            }

            initialized = true;
        }

        public void SyncFromGrid()
        {
            Initialize();
        }

        [ContextMenu("Step Ones")]
        public void StepOnce()
        {
            if (!initialized || gridManager == null || current == null)
            {
                Initialize();
                if (!initialized)
                {
                    return;
                }
            }

            Array.Copy(current, next, current.Length);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Cell sourceCell = current[x, y];
                    if (!sourceCell.isActive)
                    {
                        continue;
                    }

                    if (activeModel == null)
                    {
                        Debug.LogError("No CA_Model assigned!");
                        return;
                    }

                    SimZone evaluatedZone = activeModel.EvaluateCell(x, y, current, width, height);
                    Cell targetCell = next[x, y];
                    targetCell.currentZone = evaluatedZone;
                    targetCell.nextZone = evaluatedZone;
                    next[x, y] = targetCell;
                }
            }

            Cell[,] temp = current;
            current = next;
            next = temp;

            gridManager.SetGridReference(current);
            gridManager.MarkDirty();
        }

        public int CountType(int x, int y, int radius, SimZone type)
        {
            return CAUtils.CountType(x, y, radius, type, current, width, height);
        }

        public int CountType(int x, int y, int radius, SimZone typeA, SimZone typeB)
        {
            return CAUtils.CountType(x, y, radius, typeA, typeB, current, width, height);
        }

        public float GetDensity(int count, int radius)
        {
            return CAUtils.GetDensity(count, radius);
        }

        public bool HasType(int x, int y, int radius, SimZone type)
        {
            return CAUtils.HasType(x, y, radius, type, current, width, height);
        }

        public bool TryGetTransitionProbabilities(int x, int y, out float residentialChance, out float industrialChance)
        {
            residentialChance = 0f;
            industrialChance = 0f;

            if (!initialized || current == null)
            {
                Initialize();
                if (!initialized)
                {
                    return false;
                }
            }

            SimZone zone = current[x, y].currentZone;

            if (zone == SimZone.AGRICULTURE)
            {
                int urbanR1 = CAUtils.CountType(x, y, 1, SimZone.URBAN_DENSE, SimZone.URBAN_RESIDENTIAL, current, width, height);
                int urbanR2 = CAUtils.CountType(x, y, 2, SimZone.URBAN_DENSE, SimZone.URBAN_RESIDENTIAL, current, width, height);
                float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);

                if (urbanR1 >= 3)
                {
                    residentialChance = 1f;
                    return true;
                }

                if (urbanR1 >= 2 && urbanDensityR2 > 0.3f)
                {
                    residentialChance = 1f;
                    return true;
                }

                return true;
            }

            if (zone == SimZone.URBAN_RESIDENTIAL)
            {
                int urbanR2 = CAUtils.CountType(x, y, 2, SimZone.URBAN_DENSE, SimZone.URBAN_RESIDENTIAL, current, width, height);
                residentialChance = CAUtils.GetDensity(urbanR2, 2) >= 0.6f ? 1f : 0f;
                return true;
            }

            if (zone == SimZone.SHRUBLAND)
            {
                industrialChance = CAUtils.CountType(x, y, 1, SimZone.AGRICULTURE, current, width, height) >= 3 ? 1f : 0f;
                return true;
            }

            if (zone == SimZone.FOREST)
            {
                int forestR2 = CAUtils.CountType(x, y, 2, SimZone.FOREST, current, width, height);
                if (CAUtils.GetDensity(forestR2, 2) >= 0.6f)
                {
                    industrialChance = 0f;
                    return true;
                }

                int urbanR3 = CAUtils.CountType(x, y, 3, SimZone.URBAN_DENSE, SimZone.URBAN_RESIDENTIAL, current, width, height);
                industrialChance = CAUtils.GetDensity(urbanR3, 3) >= 0.5f ? 1f : 0f;
                return true;
            }

            return false;
        }
    }
}
