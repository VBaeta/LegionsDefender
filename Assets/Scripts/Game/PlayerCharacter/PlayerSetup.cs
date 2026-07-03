using UnityEngine;
using Photon.Pun;

public class PlayerSetup : MonoBehaviourPun
{
    public ThirdPersonMovement movement;
    public GameObject camera;

    private void Start()
    {
        if (photonView.IsMine)
        {
            IsLocalPlayer();
        }
        else
        {
            IsRemotePlayer();
        }
    }

    public void IsLocalPlayer()
    {
        if (movement != null) movement.enabled = true;
        if (camera != null) camera.SetActive(true);
    }

    public void IsRemotePlayer()
    {
        if (movement != null) movement.enabled = false;
        if (camera != null) camera.SetActive(false);
        
        // Remove or disable listener on remote players
        var listener = GetComponentInChildren<AudioListener>(true);
        if (listener != null)
        {
            listener.enabled = false;
        }
    }
}
