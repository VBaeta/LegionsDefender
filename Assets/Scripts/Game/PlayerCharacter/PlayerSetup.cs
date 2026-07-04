using UnityEngine;
using Photon.Pun;

public class PlayerSetup : MonoBehaviourPun
{
    public ThirdPersonMovement movement;
    public GameObject camera;

    [Header("Visual Swap")]
    [SerializeField] private CharacterDatabase characterDatabase;
    [SerializeField] private MeshFilter targetMeshFilter;
    [SerializeField] private MeshRenderer targetMeshRenderer;

    private void Start()
    {
        // Enforce CharacterController center based on height so bottom is always at Y = 0
        var cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.center = new Vector3(0f, cc.height / 2f, 0f);
        }

        // Self-heal: If no Animator component is found, destroy PhotonAnimatorView to prevent exceptions
        try
        {
            if (GetComponent<Animator>() == null)
            {
                var animView = GetComponent<PhotonAnimatorView>();
                if (animView != null)
                {
                    DestroyImmediate(animView);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to self-heal PhotonAnimatorView: {ex.Message}");
        }

        // Try to dynamically find camera if not assigned
        if (camera == null)
        {
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) camera = cam.gameObject;
        }

        // Safeguard visual swap to ensure network role is ALWAYS assigned
        try
        {
            ApplyCharacterVisual();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerSetup] Character visual swap failed: {ex.Message}\n{ex.StackTrace}");
        }

        if (photonView.IsMine)
        {
            IsLocalPlayer();
        }
        else
        {
            IsRemotePlayer();
        }
    }

    private void ApplyCharacterVisual()
    {
        if (photonView.Owner != null && photonView.Owner.CustomProperties.TryGetValue("SelectedCharacter", out object charNameObj))
        {
            string charName = (string)charNameObj;
            if (!string.IsNullOrEmpty(charName))
            {
                if (characterDatabase == null)
                {
                    characterDatabase = Resources.Load<CharacterDatabase>("CharacterDatabase");
                }

                if (characterDatabase != null)
                {
                    CharacterData data = characterDatabase.GetByName(charName);
                    if (data != null)
                    {
                        if (targetMeshFilter == null || targetMeshRenderer == null)
                        {
                            Transform meshTrans = transform.Find("CharMesh");
                            if (meshTrans != null)
                            {
                                targetMeshFilter = meshTrans.GetComponent<MeshFilter>();
                                targetMeshRenderer = meshTrans.GetComponent<MeshRenderer>();
                            }
                        }

                        if (targetMeshFilter != null && data.characterMesh != null)
                        {
                            targetMeshFilter.mesh = data.characterMesh;
                        }

                        if (targetMeshRenderer != null && data.characterMaterial != null)
                        {
                            targetMeshRenderer.material = data.characterMaterial;
                        }

                        // Disable placeholder sub-elements (eyes, back) so they don't float around the new mesh
                        Transform eyes = transform.Find("Capsule/Eyes");
                        if (eyes != null) eyes.gameObject.SetActive(false);

                        Transform back = transform.Find("Capsule/Back");
                        if (back != null) back.gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    public void IsLocalPlayer()
    {
        if (movement != null) movement.enabled = true;
        if (camera != null) camera.SetActive(true);

        Transform tpCam = transform.Find("ThirdPersonCamera");
        if (tpCam != null) tpCam.gameObject.SetActive(true);

        // Dynamically assign the camera reference to the movement script
        if (movement != null && camera != null)
        {
            var type = typeof(ThirdPersonMovement);
            var camField = type.GetField("camera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (camField != null)
            {
                camField.SetValue(movement, camera.transform);
            }
        }

        // Instantiate Game HUD Canvas
        GameObject hudPrefab = Resources.Load<GameObject>("Prefabs/UI/HUDCanvas");
        if (hudPrefab != null)
        {
            Instantiate(hudPrefab);
            Debug.Log("[PlayerSetup] Local UI HUD Canvas spawned successfully!");
        }
        else
        {
            Debug.LogWarning("[PlayerSetup] UI HUD Prefab not found at Prefabs/UI/HUDCanvas!");
        }
    }

    public void IsRemotePlayer()
    {
        if (movement != null) movement.enabled = false;
        if (camera != null) camera.SetActive(false);

        Transform tpCam = transform.Find("ThirdPersonCamera");
        if (tpCam != null) tpCam.gameObject.SetActive(false);
        
        // Remove or disable listener on remote players
        var listener = GetComponentInChildren<AudioListener>(true);
        if (listener != null)
        {
            listener.enabled = false;
        }
    }
}
