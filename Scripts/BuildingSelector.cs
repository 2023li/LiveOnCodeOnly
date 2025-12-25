using UnityEngine.Tilemaps;
using UnityEngine;
using System;

public class BuildingSelector : MonoBehaviour
{
    [SerializeField] private TileBase highlightTile;
    private BuildingInstance _current;

    public event Action<BuildingInstance> Event_SelectedBuilding;

    private void OnEnable()
    {
            InputManager.Instance.OnMousePrimaryClick += HandleClick;


        Event_SelectedBuilding += Test;
    }
   


    private void OnDisable()
    {
        if (InputManager.HasInstance)
        {
            InputManager.Instance.OnMousePrimaryClick -= HandleClick;
        }
        ClearHighlight();
    }

    private void HandleClick(Vector2 screenPoint)
    {
        

        if (!GridSystem.HasInstance || (InputManager.Instance?.IsBuildingMap() ?? false))
        {
            return;
        }

        CubeCoor cell = GridSystem.Instance.ScreenToCube(screenPoint);

        if (BuildingInstance.TryGetBuildingAtCell(cell, out BuildingInstance building) && building?.Self_CurrentOccupy?.Length > 0)
        {
            _current = building;
            GridSystem.Instance.SetHighlight(_current.Self_CurrentOccupy,TileLib.GetTile(GameTileEnum.Tile_黄色));
        }
        else
        {
            _current = null;
            ClearHighlight();
        }

        Event_SelectedBuilding?.Invoke(_current);

        
    }


    private void ClearHighlight()
    {
        if (GridSystem.HasInstance)
        {
            GridSystem.Instance.ClearHighlight();
        }
    }

    private void Test(BuildingInstance bud)
    {
        if (bud!=null)
        {
            Debug.Log(bud.gameObject.name);
        }
    }

}
