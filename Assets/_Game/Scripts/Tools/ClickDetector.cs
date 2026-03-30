using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class ClickDetector : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Check if clicking on UI
            if (IsPointerOverUI(out GameObject uiObject))
            {
                Debug.Log($"Clicked on UI: {uiObject.name}");
            }
            else
            {
                // Convert mouse to world position
                Vector2 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

                // Raycast in 2D
                RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

                if (hit.collider != null)
                {
                    Debug.Log($"Clicked on 2D GameObject: {hit.collider.gameObject.name}");
                }
                else
                {
                    Debug.Log("Clicked on nothing");
                }
            }
        }
    }

    private bool IsPointerOverUI(out GameObject clickedUI)
    {
        clickedUI = null;
        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        if (results.Count > 0)
        {
            clickedUI = results[0].gameObject;
            return true;
        }

        return false;
    }
}
