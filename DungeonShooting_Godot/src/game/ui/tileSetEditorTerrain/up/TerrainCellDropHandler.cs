﻿using Godot;

namespace UI.TileSetEditorTerrain;

public partial class TerrainCellDropHandler : Control
{
    /// <summary>
    /// 是否放置了图块
    /// </summary>
    public bool IsPutDownTexture { get; set; }
    
    private TerrainCell _cell;
    private TileSetEditorTerrainPanel _panel;
    
    public void Init(TerrainCell cell)
    {
        _cell = cell;
        _panel = cell.CellNode.UiPanel;
    }
    
    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.Rect2I;
    }
    
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var rect = data.AsRect2I();
        var sprite2D = _cell.CellNode.L_CellTexture.Instance;
        sprite2D.Texture = _panel.EditorPanel.Texture;
        sprite2D.RegionEnabled = true;
        sprite2D.RegionRect = rect;
        IsPutDownTexture = true;
    }

    public override void _GuiInput(InputEvent @event)
    {
        //右键擦除图块
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
        {
            AcceptEvent();
            _cell.CellNode.L_CellTexture.Instance.Texture = null;
            IsPutDownTexture = false;
        }
    }
}