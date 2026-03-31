using UnityEngine;

public class PlayerStartPositioner : MonoBehaviour
{
    [Tooltip("The transform that marks where the player should start")]
    public Transform playerStartPoint;

    [Tooltip("Optional: If you want to automatically find the player rather than setting it in the inspector")]
    public bool autoFindPlayer = true;

    [Tooltip("The player GameObject - leave empty if autoFindPlayer is true")]
    public GameObject player;

    [Tooltip("Should the player's Y rotation match the start point's rotation?")]
    public bool matchRotation = true;

    [Tooltip("Should the player be teleported to the start position on game reset/restart?")]
    public bool teleportOnReset = true;

    void Start()
    {
        if (player == null && autoFindPlayer)
        {
            // Try to find player by tag
            player = GameObject.FindGameObjectWithTag("Player");

            // If still null, try to find PlayerController component
            if (player == null)
            {
                PlayerController playerController = FindObjectOfType<PlayerController>();
                if (playerController != null)
                {
                    player = playerController.gameObject;
                    Debug.Log("Found player via PlayerController component");
                }
                else
                {
                    Debug.LogWarning("Could not find player automatically. Please assign it in the inspector or tag your player GameObject with 'Player'.");
                }
            }
            else
            {
                Debug.Log("Found player via 'Player' tag");
            }
        }

        if (playerStartPoint == null)
        {
            // Try to find a GameObject named "PlayerStart" if not set
            GameObject startObj = GameObject.Find("PlayerStart");
            if (startObj != null)
            {
                playerStartPoint = startObj.transform;
                Debug.Log("Found PlayerStart object by name");
            }
            else
            {
                Debug.LogWarning("Player start point not assigned. Player will start at its default position.");
                return;
            }
        }

        PositionPlayerAtStart();
    }

    public void PositionPlayerAtStart()
    {
        if (player != null && playerStartPoint != null)
        {
            // Set position
            player.transform.position = playerStartPoint.position;

            // Set rotation if desired
            if (matchRotation)
            {
                // Only match Y rotation (keep player upright)
                float yRotation = playerStartPoint.eulerAngles.y;
                player.transform.rotation = Quaternion.Euler(0, yRotation, 0);
            }

            Debug.Log($"Positioned player at {playerStartPoint.name}: {playerStartPoint.position}");
        }
        else
        {
            Debug.LogError("Cannot position player: player or start point is null");
        }
    }

    // This can be called from other scripts, like a game reset manager
    public void ResetPlayerPosition()
    {
        if (teleportOnReset)
        {
            PositionPlayerAtStart();
        }
    }
}