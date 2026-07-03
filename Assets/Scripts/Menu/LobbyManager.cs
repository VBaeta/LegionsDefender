using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    public static LobbyManager Instance { get; private set; }

    [Header("Spawn Configuration")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private GameObject playerSlotPrefab;

    [Header("Database")]
    [SerializeField] private CharacterDatabase characterDatabase;

    [Header("Room UI References")]
    [SerializeField] private TMP_Text roomTitleText;
    [SerializeField] private GameObject startGameButton;
    [SerializeField] private GameObject readyButton;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private Button selectCharacterButton;
    [SerializeField] private CharacterSelectionPanel characterSelectionPanel;

    [Header("Game Mode UI")]
    [SerializeField] private Button coopButton;
    [SerializeField] private Button teamVsTeamButton;
    [SerializeField] private GameObject coopHighlight;
    [SerializeField] private GameObject teamVsTeamHighlight;

    private PlayerSlotDisplay[] _spawnedSlots;
    private bool _isLocalPlayerReady = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Setup local slot instances at the spawn points
        _spawnedSlots = new PlayerSlotDisplay[spawnPoints.Length];
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            GameObject slotGo = Instantiate(playerSlotPrefab, spawnPoints[i].position, spawnPoints[i].rotation);
            _spawnedSlots[i] = slotGo.GetComponent<PlayerSlotDisplay>();
            _spawnedSlots[i].SetEmpty();
        }

        // Add UI listeners
        if (coopButton != null) coopButton.onClick.AddListener(() => SetGameMode("CoOp"));
        if (teamVsTeamButton != null) teamVsTeamButton.onClick.AddListener(() => SetGameMode("TeamVsTeam"));
    }

    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            roomTitleText.text = PhotonNetwork.CurrentRoom.Name;
            
            // Set initial local properties
            ExitGames.Client.Photon.Hashtable initialProps = new ExitGames.Client.Photon.Hashtable();
            initialProps["IsReady"] = false;
            initialProps["SelectedCharacter"] = "";
            PhotonNetwork.LocalPlayer.SetCustomProperties(initialProps);

            // If master client, set default game mode
            if (PhotonNetwork.IsMasterClient)
            {
                SetGameMode("CoOp");
            }

            UpdateSlots();
        }
    }

    public string GetLocalPlayerSelectedCharacter()
    {
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("SelectedCharacter", out object charObj))
        {
            return (string)charObj;
        }
        return "";
    }

    public void SelectCharacter(string characterName)
    {
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props["SelectedCharacter"] = characterName;
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    public void ToggleReady()
    {
        // Don't allow readying up without selecting a character first
        if (!_isLocalPlayerReady)
        {
            string selectedChar = GetLocalPlayerSelectedCharacter();
            if (string.IsNullOrEmpty(selectedChar))
            {
                Debug.Log("You must select a character before readying up!");
                return;
            }
        }

        _isLocalPlayerReady = !_isLocalPlayerReady;
        
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props["IsReady"] = _isLocalPlayerReady;
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        if (readyButtonText != null)
        {
            readyButtonText.text = _isLocalPlayerReady ? "Cancel" : "Ready";
        }

        // Disable/enable character selection depending on ready state
        if (selectCharacterButton != null)
        {
            selectCharacterButton.interactable = !_isLocalPlayerReady;
        }
    }

    public void SetGameMode(string mode)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props["GameMode"] = mode;
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    private void UpdateSlots()
    {
        if (_spawnedSlots == null) return;

        // Clear all slots first
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            _spawnedSlots[i].SetEmpty();
        }

        // Sort players by join order (ActorNumber)
        List<Player> players = new List<Player>(PhotonNetwork.PlayerList);
        players.Sort((p1, p2) => p1.ActorNumber.CompareTo(p2.ActorNumber));

        for (int i = 0; i < players.Count && i < spawnPoints.Length; i++)
        {
            Player p = players[i];
            PlayerSlotDisplay slot = _spawnedSlots[i];

            bool isReady = false;
            if (p.CustomProperties.TryGetValue("IsReady", out object readyObj))
            {
                isReady = (bool)readyObj;
            }

            // Host is automatically marked as ready visually (green name) or can be neutral
            // Let's make master client always display as ready (green name) since they can start, or show as not ready if they didn't select character.
            // Actually, showing host as green when they choose a character or just checking if they selected character is good.
            // Let's show green for host, since they don't have a ready button, or just base ready status on property.
            bool isHost = p.IsMasterClient;
            slot.SetPlayer(p.NickName, isHost ? true : isReady);

            if (p.CustomProperties.TryGetValue("SelectedCharacter", out object charObj) && charObj != null)
            {
                string charName = (string)charObj;
                if (!string.IsNullOrEmpty(charName))
                {
                    CharacterData charData = characterDatabase.GetByName(charName);
                    if (charData != null)
                    {
                        slot.SetCharacter(charData);
                    }
                    else
                    {
                        slot.ClearCharacter();
                    }
                }
                else
                {
                    slot.ClearCharacter();
                }
            }
            else
            {
                slot.ClearCharacter();
            }
        }

        UpdateUIState();
    }

    private void UpdateUIState()
    {
        bool isHost = PhotonNetwork.IsMasterClient;

        // Show/hide Start / Ready button
        if (startGameButton != null) startGameButton.SetActive(isHost);
        if (readyButton != null) readyButton.SetActive(!isHost);

        // Game mode selector interactivity
        if (coopButton != null) coopButton.interactable = isHost;
        if (teamVsTeamButton != null) teamVsTeamButton.interactable = isHost;

        // Update selected game mode visual highlights
        string gameMode = "CoOp";
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("GameMode", out object modeObj))
        {
            gameMode = (string)modeObj;
        }

        if (coopHighlight != null) coopHighlight.SetActive(gameMode == "CoOp");
        if (teamVsTeamHighlight != null) teamVsTeamHighlight.SetActive(gameMode == "TeamVsTeam");

        // Host validation: Enable start game button only if everyone has character and clients are ready
        if (isHost && startGameButton != null)
        {
            startGameButton.GetComponent<Button>().interactable = CheckStartGameConditions();
        }
    }

    private bool CheckStartGameConditions()
    {
        if (PhotonNetwork.PlayerList.Length == 0) return false;

        foreach (var p in PhotonNetwork.PlayerList)
        {
            // Must have chosen a character
            if (!p.CustomProperties.TryGetValue("SelectedCharacter", out object charObj) || charObj == null || string.IsNullOrEmpty((string)charObj))
            {
                return false;
            }

            // If client, must be ready
            if (!p.IsMasterClient)
            {
                if (!p.CustomProperties.TryGetValue("IsReady", out object readyObj) || !(bool)readyObj)
                {
                    return false;
                }
            }
        }
        return true;
    }

    public void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        string gameMode = "CoOp";
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("GameMode", out object modeObj))
        {
            gameMode = (string)modeObj;
        }

        if (gameMode == "TeamVsTeam")
        {
            PhotonNetwork.LoadLevel("TvTGameScene");
        }
        else
        {
            PhotonNetwork.LoadLevel("CoopGameScene");
        }
    }

    public void QuitRoom()
    {
        PhotonNetwork.LeaveRoom();
    }

    #region Photon Callbacks

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateSlots();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateSlots();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        UpdateSlots();
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        UpdateUIState();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        UpdateSlots();
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene("MenuScene");
    }

    #endregion
}
