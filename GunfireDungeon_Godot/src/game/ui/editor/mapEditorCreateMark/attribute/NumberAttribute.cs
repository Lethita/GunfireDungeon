﻿using System.Globalization;
using Godot;

using DsUi;

namespace UI.editor.MapEditorCreateMark;

public partial class NumberAttribute : AttributeBase
{
    private MapEditorCreateMark.NumberBar _numberBar;

    public override void SetUiNode(IUiNode uiNode)
    {
        _numberBar = (MapEditorCreateMark.NumberBar)uiNode;
    }

    public override void OnDestroy()
    {
        
    }

    public override string GetAttributeValue()
    {
        return _numberBar.L_NumInput.Instance.Value.ToString(CultureInfo.InvariantCulture);
    }
}