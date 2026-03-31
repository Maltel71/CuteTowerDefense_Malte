using UnityEngine;

public class UpgradePanelPosition : MonoBehaviour
{
    [Tooltip("Set to true to show a visual marker in the editor")]
    public bool showPositionGizmo = true;

    [Tooltip("Size of the position gizmo in the editor")]
    public float gizmoSize = 0.25f;

    [Tooltip("Color of the position gizmo")]
    public Color gizmoColor = Color.cyan;

    private void OnDrawGizmos()
    {
        if (showPositionGizmo)
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, gizmoSize);

            // Draw a line upward to better visualize the position
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * gizmoSize * 2);
        }
    }
}