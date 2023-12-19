
using System.Collections.Generic;
using Godot;

/// <summary>
/// 导入的组合图块数据
/// </summary>
public class ImportCombinationData
{
    /// <summary>
    /// 预览图
    /// </summary>
    public ImageTexture PreviewTexture { get; set; }
    /// <summary>
    /// 组合图块数据
    /// </summary>
    public TileCombinationInfo CombinationInfo { get; set; }

    public ImportCombinationData(ImageTexture previewTexture, TileCombinationInfo combinationInfo)
    {
        PreviewTexture = previewTexture;
        CombinationInfo = combinationInfo;
    }
    
    public void UpdatePreviewTexture(Image src)
    {
        using (var image = GetPreviewTexture(src, CombinationInfo.Cells, CombinationInfo.Positions))
        {
            PreviewTexture.SetImage(image);
        }
    }
    
    public static Image GetPreviewTexture(Image src, SerializeVector2[] cells, SerializeVector2[] positions)
    {
        var rect = Utils.CalcTileRect(positions);
        var rectSize = rect.Size;
        var image = Image.Create(rectSize.X + 4, rectSize.Y + 4, false, Image.Format.Rgba8);
        image.Fill(Colors.Gray);
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            var pos = positions[i];
            image.BlendRect(src, new Rect2I(cell.AsVector2I(), GameConfig.TileCellSizeVector2I), pos.AsVector2I() + new Vector2I(2, 2));
        }
        return image;
    }
}