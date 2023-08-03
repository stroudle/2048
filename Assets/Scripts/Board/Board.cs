using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Board : MonoBehaviour
{
    public delegate void GameOver();
    public event GameOver OnGameOver;

    public delegate void IncreaseScore(int value);
    public event IncreaseScore OnIncreaseScore;

    [SerializeField]
    private Tile _tilePrefab;
    [SerializeField]
    private GameObject _tilesParent;

    private CellGrid _grid;
    private List<Tile> _tiles;
    private WeightedRandomGenerator _rng;

    private void Awake()
    {
        _grid = GetComponentInChildren<CellGrid>();
        _tiles = new List<Tile>(16);

        _rng = new WeightedRandomGenerator();
        _rng.AddNumberWithWeight(2, 95.0f);
        _rng.AddNumberWithWeight(4, 5.0f);
    }

    private void OnEnable()
    {
        KeyboardInput.OnKeyboardInput += OnInputRecievedHandler;
        MobileInput.OnMobileInput += OnInputRecievedHandler;
    }

    private void OnDisable() 
    {
        KeyboardInput.OnKeyboardInput -= OnInputRecievedHandler;
        MobileInput.OnMobileInput += OnInputRecievedHandler;
    }

    public void ClearBoard()
    {
        foreach (var cell in _grid.Cells) 
        {
            if(cell.Tile != null)
            {
                Destroy(cell.Tile.gameObject);
            }

            cell.Tile = null;
        }

        _tiles.Clear();
    }

    public void SpawnTile()
    {
        Tile tile = Instantiate(_tilePrefab, _tilesParent.transform);
        tile.Spawn(_grid.GetRandomEmptyCell(), _rng.GetRandomNumber());
        _tiles.Add(tile);
    }

    private void OnInputRecievedHandler(Vector2Int input)
    {
        MoveTiles(input);
    }

    private void MoveTiles(Vector2Int direction)
    {
        int xStart = direction.x == 1 ? (_grid.Width - 1) : (direction.x == -1 ? 0 : 0);
        int xIncrement = direction.x == 1 ? -1 : (direction.x == -1 ? 1 : 1);

        int yStart = direction.y == 1 ? 0 : (direction.y == -1 ? (_grid.Height - 1) : 0);
        int yIncrement = direction.y == 1 ? 1 : (direction.y == -1 ? -1 : 1);

        bool boardChanged = false;
        for(int x = xStart; x >= 0 && x < _grid.Width; x += xIncrement)
        {
            for(int y = yStart; y >= 0 && y < _grid.Height; y += yIncrement)
            {
                Cell cell = _grid.GetCell(x, y);

                if(cell.Occupied)
                {
                    boardChanged |= MoveTile(cell.Tile, direction);
                }
            }
        }

        if(boardChanged)
        {
            StartCoroutine(WaitForChanges());
        }
    }

    private IEnumerator WaitForChanges()
    {
        InputBlocker.BlockInputs();
        yield return new WaitForSeconds(Tile.MovementDuration);
        InputBlocker.UnblockInputs();
        FinalizeRound();
    }

    private void FinalizeRound()
    {
        foreach(var tile in _tiles)
        {
            tile.CanMerge = true;
        }

        if(_tiles.Count != _grid.Size)
        {
            SpawnTile();
        }

        if(CheckForGameOver())
        {
            OnGameOver?.Invoke();
        }
    }

    private bool CheckForGameOver()
    {
        if(_tiles.Count != _grid.Size)
        {
            return false;
        }

        foreach(var tile in _tiles)
        {
            Cell up = _grid.GetAdjacentCell(tile.Cell, Vector2Int.up);
            Cell down = _grid.GetAdjacentCell(tile.Cell, Vector2Int.down);
            Cell left = _grid.GetAdjacentCell(tile.Cell, Vector2Int.left);
            Cell right = _grid.GetAdjacentCell(tile.Cell, Vector2Int.right);

            if(up != null && CanMerge(tile, up.Tile))
            {
                return false;
            }
            if(down != null && CanMerge(tile, down.Tile))
            {
                return false;
            }
            if(left != null && CanMerge(tile, left.Tile))
            {
                return false;
            }
            if(right != null && CanMerge(tile, right.Tile))
            {
                return false;
            }
        }

        return true;
    }

    private bool MoveTile(Tile tile, Vector2Int direction)
    {
        Cell destination = null;
        Cell adjacent = _grid.GetAdjacentCell(tile.Cell, direction);

        while(adjacent != null)
        {
            if(adjacent.Occupied)
            {
                if(CanMerge(tile, adjacent.Tile))
                {
                    Merge(tile, adjacent.Tile);
                    return true;
                }
                break;
            }

            destination  = adjacent;
            adjacent = _grid.GetAdjacentCell(adjacent, direction);
        }

        if(destination != null)
        {
            tile.MoveTo(destination);
            return true;
        }
        return false;
    }

    private bool CanMerge(Tile a, Tile b)
    {
        return a.TileValue == b.TileValue && b.CanMerge;
    }

    private void Merge(Tile a, Tile b)
    {
        _tiles.Remove(a);
        a.Merge(b.Cell);

        int value = b.TileValue * 2;
        b.SetTileValue(value);

        OnIncreaseScore?.Invoke(value);
    }
}
