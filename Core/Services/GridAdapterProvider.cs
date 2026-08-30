using UnityEngine;

namespace ONIModPack.Core.Services
{
    public class GameGridAdapter : ServiceBase, IGridAdapter
    {
        public override string ServiceName => "GameGridAdapter";

        public int Width => Grid.WidthInCells;
        public int Height => Grid.HeightInCells;
        public int CellCount => Grid.WidthInCells * Grid.HeightInCells;

        public int XYToCell(int x, int y) => Grid.XYToCell(x, y);
        public void CellToXY(int cell, out int x, out int y) => Grid.CellToXY(cell, out x, out y);
        public bool IsValidCell(int cell) => Grid.IsValidCell(cell);
        public int PosToCell(Vector3 pos) => Grid.PosToCell(pos);
        public GameObject GetCellObject(int cell, int layer) => Grid.Objects[cell, layer];
    }

    public interface IGridAdapter : IService
    {
        int Width { get; }
        int Height { get; }
        int CellCount { get; }

        int XYToCell(int x, int y);
        void CellToXY(int cell, out int x, out int y);
        bool IsValidCell(int cell);
        int PosToCell(Vector3 pos);
        GameObject GetCellObject(int cell, int layer);
    }
}