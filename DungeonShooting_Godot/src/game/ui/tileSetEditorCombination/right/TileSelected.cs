﻿using Godot;

namespace UI.TileSetEditorCombination;

public partial class TileSelected : VBoxContainer, IUiNodeScript
{
    private TileSetEditorCombination.RightBg _rightBg;
    private UiGrid<TileSetEditorCombination.CellButton, ImportCombinationData> _grid;
    
    public void SetUiNode(IUiNode uiNode)
    {
        _rightBg = (TileSetEditorCombination.RightBg)uiNode;

        _grid = new UiGrid<TileSetEditorCombination.CellButton, ImportCombinationData>(_rightBg.L_ScrollContainer.L_CellButton, typeof(TileCell));
        _grid.SetCellOffset(new Vector2I(5, 5));
        _grid.SetAutoColumns(true);
        _grid.SetHorizontalExpand(true);
        
        _rightBg.UiPanel.AddEventListener(EventEnum.OnImportCombination, OnImportCombination);
        _rightBg.UiPanel.AddEventListener(EventEnum.OnRemoveCombination, OnRemoveCombination);
        _rightBg.UiPanel.AddEventListener(EventEnum.OnUpdateCombination, OnUpdateCombination);
    }

    /// <summary>
    /// 导入组合图块
    /// </summary>
    private void OnImportCombination(object obj)
    {
        if (obj is ImportCombinationData data)
        {
            _grid.Add(data);
            _grid.Sort();
        }
    }
    
    /// <summary>
    /// 移除组合图块
    /// </summary>
    private void OnRemoveCombination(object obj)
    {
        if (obj is ImportCombinationData data)
        {
            var uiCell = _grid.Find(c => c.Data.CombinationInfo.Id == data.CombinationInfo.Id);
            if (uiCell != null)
            {
                _grid.RemoveByIndex(uiCell.Index);
            }
        }
    }

    /// <summary>
    /// 修改组合图块
    /// </summary>
    private void OnUpdateCombination(object obj)
    {
        if (obj is ImportCombinationData data)
        {
            var uiCell = _grid.Find(c => c.Data.CombinationInfo.Id == data.CombinationInfo.Id);
            if (uiCell != null)
            {
                uiCell.SetData(data);
            }
        }
    }

    public void OnDestroy()
    {
        _grid.Destroy();
    }
    

    /// <summary>
    /// 改变TileSet纹理
    /// </summary>
    public void OnChangeTileSetTexture()
    {
        //_grid.RemoveAll();
        //刷新预览图
        _grid.ForEach(cell =>
        {
            cell.Data.UpdatePreviewTexture(_rightBg.UiPanel.EditorPanel.TextureImage);
        });
    }
}