using Godot;

namespace UI.TileSetEditorProject;

public partial class TileSetEditorProjectPanel : TileSetEditorProject
{

    private UiGrid<TileButton, TileSetInfo> _grid;
    
    public override void OnCreateUi()
    {
        S_Back.Instance.Visible = PrevUi != null;
        S_Back.Instance.Pressed += () =>
        {
            OpenPrevUi();
        };

        _grid = new UiGrid<TileButton, TileSetInfo>(S_TileButton, typeof(TileButtonCell));
        _grid.SetAutoColumns(true);
        _grid.SetCellOffset(new Vector2I(10, 10));
        _grid.SetHorizontalExpand(true);
        
        var tileSetInfo = new TileSetInfo();
        tileSetInfo.InitData();
        tileSetInfo.Name = "测试数据";
        _grid.Add(tileSetInfo);
    }

    public override void OnDestroyUi()
    {
        _grid.Destroy();
    }

}
