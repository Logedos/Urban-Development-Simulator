using System.Collections;
using Simulation;
using UnityEngine;

public class SimulationController : MonoBehaviour
{
    public CASimulator caSimulator;
    public SimulationUI simulationUI;
    //public SimulationStatistics simulationStatistics;

    public bool isRunning;
    public int stepsPerFrame = 1;
    public int ticksPerYear = 1;
    public int currentTick = 0;
    public int startYear = 2000;
    public int currentYear;
    public float targetFPS = 30f;

    private Coroutine simulationCoroutine;
    private WaitForSeconds loopDelay;
    private Cell[,] originalSnapshot;

    private IEnumerator Start()
    {
        currentYear = startYear;
        UpdateLoopDelay();
        yield return CaptureSnapshotWhenReady();
        //UpdateStatistics();
        UpdateYearUI(currentYear);
        if (simulationUI != null)
        {
            simulationUI.RefreshState();
        }
    }

    public IEnumerator SimulationLoop()
    {
        while (isRunning)
        {
            for (int i = 0; i < stepsPerFrame; i++)
            {
                caSimulator.StepOnce();
                currentTick++;
            }

           // UpdateStatistics();

            currentYear = startYear + (currentTick / Mathf.Max(1, ticksPerYear));
            UpdateYearUI(currentYear);

            yield return loopDelay;
        }
    }

    public void Play()
    {
        if (isRunning || caSimulator == null)
        {
            return;
        }

        isRunning = true;
        UpdateLoopDelay();
        simulationCoroutine = StartCoroutine(SimulationLoop());
        if (simulationUI != null)
        {
            simulationUI.RefreshState();
        }
    }

    public void Pause()
    {
        if (!isRunning)
        {
            return;
        }

        isRunning = false;
        if (simulationCoroutine != null)
        {
            StopCoroutine(simulationCoroutine);
            simulationCoroutine = null;
        }

        if (simulationUI != null)
        {
            simulationUI.RefreshState();
        }
    }

    public void TogglePlayPause()
    {
        if (isRunning)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    public void StepOnce()
    {
        if (isRunning || caSimulator == null)
        {
            return;
        }

        caSimulator.StepOnce();
        currentTick++;
        currentYear = startYear + (currentTick / Mathf.Max(1, ticksPerYear));
      //  UpdateStatistics();
        UpdateYearUI(currentYear);
        if (simulationUI != null)
        {
            simulationUI.RefreshState();
        }
    }

    public void ResetSimulation()
    {
        Pause();

        GridManager gridManager = GridManager.Instance;
        if (gridManager == null)
        {
            return;
        }

        if (originalSnapshot == null)
        {
            originalSnapshot = CloneGrid(gridManager.GetGridReference());
        }

        Cell[,] restored = CloneGrid(originalSnapshot);
        gridManager.SetGridReference(restored);
        if (caSimulator != null)
        {
            caSimulator.SyncFromGrid();
        }

        currentTick = 0;
        currentYear = startYear;
        //UpdateStatistics();
        UpdateYearUI(currentYear);
        gridManager.MarkDirty();

        if (simulationUI != null)
        {
            simulationUI.RefreshState();
        }
    }

    public void SetStepsPerFrame(int value)
    {
        stepsPerFrame = Mathf.Max(1, value);
    }

    public void SetTargetFPS(float fps)
    {
        targetFPS = Mathf.Max(1f, fps);
        UpdateLoopDelay();
    }

  /*  public void UpdateStatistics()
    {
        if (simulationStatistics != null && GridManager.Instance != null)
        {
            simulationStatistics.Refresh(GridManager.Instance.GetGridReference(), currentTick);
        }
    }*/

    public void UpdateYearUI(int year)
    {
        if (simulationUI != null)
        {
            simulationUI.SetYear(year);
        }
    }

    private void UpdateLoopDelay()
    {
        loopDelay = new WaitForSeconds(1f / Mathf.Max(1f, targetFPS));
    }

    private IEnumerator CaptureSnapshotWhenReady()
    {
        GridManager gridManager = GridManager.Instance;
        int attempts = 0;

        while (attempts < 120)
        {
            gridManager = GridManager.Instance;
            if (gridManager != null)
            {
                Cell[,] grid = gridManager.GetGridReference();
                if (grid != null && HasMeaningfulData(grid, gridManager.width, gridManager.height))
                {
                    originalSnapshot = CloneGrid(grid);
                    yield break;
                }
            }

            attempts++;
            yield return null;
        }

        if (gridManager != null)
        {
            originalSnapshot = CloneGrid(gridManager.GetGridReference());
        }
    }

    private static bool HasMeaningfulData(Cell[,] grid, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (grid[x, y].currentZone != SimZone.NODATA)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Cell[,] CloneGrid(Cell[,] source)
    {
        if (source == null)
        {
            return null;
        }

        int width = source.GetLength(0);
        int height = source.GetLength(1);
        Cell[,] clone = new Cell[width, height];
        System.Array.Copy(source, clone, source.Length);
        return clone;
    }
}
